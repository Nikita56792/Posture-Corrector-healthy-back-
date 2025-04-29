import asyncio
import json
import math
import platform
import re
import subprocess
import sys
import threading
from collections import deque
from dataclasses import dataclass
from pathlib import Path
from time import monotonic
from typing import Deque, List, Optional, Tuple

import cv2
import mediapipe as mp
import numpy as np
import tkinter as tk
from tkinter import ttk, messagebox

# ──────────────────────────── Константы ────────────────────────────
PAIR_BCAST_PORT = 9500          # на этот порт шлём широковещание
PAIR_QUERY      = b'PAIR_REQ'   # запрос на сопряжение
PAIR_ACK_RE     = rb'^PAIR_ACK (?P<port>\d+)$'
PING            = b'PING'
PONG            = b'PONG'
WIFI_LIST_TAG   = 'wifi_list'
HEARTBEAT_EVERY = 5             # сек между PING

ANGLE_SMOOTH_K  = 0.15          # шаг эксп. сглаживания базового угла
DEFAULT_TOLER   = 3.0           # отклонение в градусах
ALARM_COOLDOWN  = 10            # сек между окнами предупреждений

# ──────────────────────────── Вспомогательные типы ────────────────────────────
@dataclass
class SmartphonePeer:
    ip: str
    port: int
    last_seen: float = monotonic()

# ──────────────────────────── Утилиты ────────────────────────────
def calculate_angle(a: Tuple[float, float],
                    b: Tuple[float, float],
                    c: Tuple[float, float]) -> float:
    ang = math.degrees(
        abs(math.atan2(c[1] - b[1], c[0] - b[0]) -
            math.atan2(a[1] - b[1], a[0] - b[0])))
    return ang if ang <= 180 else 360 - ang


def scan_wifi_windows() -> List[dict]:
    out = subprocess.check_output(
        ['netsh', 'wlan', 'show', 'networks', 'mode=bssid'],
        encoding='cp866', errors='ignore')
    blocks = re.split(r'\n\s*\n', out)
    nets = []
    for blk in blocks:
        m_ssid = re.search(r'SSID\s+\d+\s+: (.+)', blk)
        m_sig  = re.search(r'Сигнал\s+: (\d+)%', blk)
        if m_ssid and m_sig:
            ssid = m_ssid.group(1).strip()
            rssi = int(m_sig.group(1))
            nets.append({'ssid': ssid, 'rssi': rssi})
    return nets


def scan_wifi_linux() -> List[dict]:
    out = subprocess.check_output(
        ['nmcli', '-f', 'SSID,SIGNAL', 'dev', 'wifi'],
        encoding='utf8', errors='ignore')
    nets = []
    for line in out.splitlines()[1:]:
        if not line.strip():
            continue
        ssid, sig = line.rsplit(None, 1)
        nets.append({'ssid': ssid.strip(), 'rssi': int(sig)})
    return nets


def scan_wifi_macos() -> List[dict]:
    airport = (
        "/System/Library/PrivateFrameworks/Apple80211.framework"
        "/Versions/Current/Resources/airport"
    )
    out = subprocess.check_output([airport, '-s'], encoding='utf8')
    nets = []
    for line in out.splitlines()[1:]:
        parts = re.split(r'\s{2,}', line.strip())
        if len(parts) >= 3:
            ssid, rssi = parts[0], int(parts[2])
            nets.append({'ssid': ssid, 'rssi': rssi})
    return nets


def get_wifi_list() -> List[dict]:
    try:
        sysname = platform.system()
        if sysname == 'Windows':
            return scan_wifi_windows()
        if sysname == 'Linux':
            return scan_wifi_linux()
        if sysname == 'Darwin':
            return scan_wifi_macos()
    except Exception as e:
        print('Wi-Fi scan failed:', e)
    return []


# ──────────────────────────── Сетевой слой ─────────────────────────
class UdpService:
    """Async-UDP service: открывает сокет, управляет сопряжением
       и отправляет данные смартфону."""
    def __init__(self, gui_callback):
        self.loop        = asyncio.new_event_loop()
        self.sock        = None
        self.peer: Optional[SmartphonePeer] = None
        self.gui_cb      = gui_callback  # обновить статус
        threading.Thread(target=self.loop.run_forever,
                         daemon=True).start()
        asyncio.run_coroutine_threadsafe(self._open_socket(), self.loop)

    async def _open_socket(self):
        self.sock = asyncio.DatagramProtocol()
        transport, _ = await self.loop.create_datagram_endpoint(
            lambda: self.sock, local_addr=('0.0.0.0', 0), allow_broadcast=True)
        self.transport = transport
        # стартуем цикл широковещ. опроса
        self.loop.create_task(self._pairing_loop())
        # стартуем цикл чтения входящих
        self.loop.create_task(self._listen_loop())

    async def _pairing_loop(self):
        while self.peer is None:
            self.transport.sendto(PAIR_QUERY,
                                  ('255.255.255.255', PAIR_BCAST_PORT))
            self.gui_cb('Поиск устройства...')
            await asyncio.sleep(2)

    async def _listen_loop(self):
        while True:
            data, addr = await self.loop.sock_recvfrom(
                self.transport.get_extra_info('socket'), 1024)
            if self.peer is None:
                m = re.match(PAIR_ACK_RE, data)
                if m:
                    port = int(m.group('port'))
                    self.peer = SmartphonePeer(addr[0], port)
                    self.gui_cb(f'Устройство найдено: {addr[0]}:{port}')
                    self.loop.create_task(self._heartbeat_loop())
                    self.loop.create_task(self._send_wifi_list())
            else:
                if data == PONG:
                    self.peer.last_seen = monotonic()

    async def _heartbeat_loop(self):
        while self.peer:
            # проверяем таймаут
            if monotonic() - self.peer.last_seen > HEARTBEAT_EVERY * 3:
                self.gui_cb('Устройство не отвечает, поиск…')
                self.peer = None
                self.loop.create_task(self._pairing_loop())
                return
            self.transport.sendto(PING, (self.peer.ip, self.peer.port))
            await asyncio.sleep(HEARTBEAT_EVERY)

    async def _send_wifi_list(self):
        await asyncio.sleep(1)
        nets = get_wifi_list()
        pkt = json.dumps({'type': WIFI_LIST_TAG, 'payload': nets}).encode()
        if self.peer:
            self.transport.sendto(pkt, (self.peer.ip, self.peer.port))
            self.gui_cb(f'📡 Отправлено {len(nets)} точек Wi-Fi')

    def send_json(self, obj: dict):
        if self.peer:
            pkt = json.dumps(obj).encode()
            asyncio.run_coroutine_threadsafe(
                self._async_send(pkt), self.loop)

    async def _async_send(self, data: bytes):
        self.transport.sendto(data, (self.peer.ip, self.peer.port))


# ──────────────────────────── Модуль позы ──────────────────────────
class PoseWorker(threading.Thread):
    """Поток захвата с веб-камеры и вычисления угла."""
    def __init__(self, gui_angle_cb, gui_alarm_cb):
        super().__init__(daemon=True)
        self.gui_angle_cb = gui_angle_cb
        self.gui_alarm_cb = gui_alarm_cb
        self._stop = threading.Event()
        self.cap = cv2.VideoCapture(0, cv2.CAP_DSHOW
                                    if platform.system() == 'Windows'
                                    else 0)
        self.pose = mp.solutions.pose.Pose(
            min_detection_confidence=0.6,
            min_tracking_confidence=0.6)
        self.baseline: Optional[float] = None
        self.last_alarm: float = 0

    def run(self):
        while not self._stop.is_set():
            ret, frame = self.cap.read()
            if not ret:
                continue
            img_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            res = self.pose.process(img_rgb)
            if res.pose_landmarks:
                lms = res.pose_landmarks.landmark
                sh = (lms[mp.solutions.pose.PoseLandmark.LEFT_SHOULDER.value].x,
                      lms[mp.solutions.pose.PoseLandmark.LEFT_SHOULDER.value].y)
                hip = (lms[mp.solutions.pose.PoseLandmark.LEFT_HIP.value].x,
                       lms[mp.solutions.pose.PoseLandmark.LEFT_HIP.value].y)
                knee = (lms[mp.solutions.pose.PoseLandmark.LEFT_KNEE.value].x,
                        lms[mp.solutions.pose.PoseLandmark.LEFT_KNEE.value].y)
                angle = calculate_angle(sh, hip, knee)
                cv2.putText(frame, f'{angle:.1f}°', (20, 40),
                            cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                self.gui_angle_cb(angle)
                self._process_angle(angle)
            cv2.imshow('Pose', frame)
            if cv2.waitKey(1) & 0xFF == 27:
                self.stop()

    def _process_angle(self, angle: float):
        if self.baseline is None:
            self.baseline = angle
            return
        # экспоненциальное сглаживание
        self.baseline += ANGLE_SMOOTH_K * (angle - self.baseline)
        if abs(angle - self.baseline) > DEFAULT_TOLER:
            now = monotonic()
            if now - self.last_alarm > ALARM_COOLDOWN:
                self.last_alarm = now
                self.gui_alarm_cb(angle, self.baseline)

    def stop(self):
        self._stop.set()
        self.cap.release()
        cv2.destroyAllWindows()


# ───────────────────────────── GUI слой ────────────────────────────
class AppGUI:
    def __init__(self, root: tk.Tk):
        self.root = root
        root.title('Spine Pose Detector 2.0')
        root.geometry('380x300')
        root.protocol('WM_DELETE_WINDOW', self.on_close)
        # ttk-стили
        ttk.Style().theme_use('clam')

        self.lbl_angle = ttk.Label(root, text='Угол: —', font=('Segoe UI', 14))
        self.lbl_angle.pack(pady=10)

        self.lbl_status = ttk.Label(root, text='Статус сети: —')
        self.lbl_status.pack()

        frm_btns = ttk.Frame(root)
        frm_btns.pack(pady=10)
        self.btn_calib = ttk.Button(frm_btns, text='Калибровка',
                                    command=self.calibrate)
        self.btn_calib.pack(side='left', padx=5)
        self.btn_recalib = ttk.Button(frm_btns, text='Сброс',
                                      command=self.recalibrate)
        self.btn_recalib.pack(side='left', padx=5)

        # сетевой сервис и поток позы
        self.udp = UdpService(self.update_status)
        self.pose_worker = PoseWorker(self.update_angle, self.show_alarm)
        self.pose_worker.start()

    # ───── GUI callbacks ────────────────────────────────────────────
    def update_angle(self, angle: float):
        self.lbl_angle.config(text=f'Угол: {angle:.1f}°')

    def update_status(self, text: str):
        self.lbl_status.config(text=f'Статус сети: {text}')

    def calibrate(self):
        self.pose_worker.baseline = None
        messagebox.showinfo('Калибровка',
                            'Примите ровное положение и нажмите ОК.')

    def recalibrate(self):
        self.pose_worker.baseline = None
        messagebox.showinfo('Перекалибровка',
                            'Базовый угол сброшен. '
                            'Нажмите «Калибровка» для новой настройки.')

    def show_alarm(self, angle: float, base: float):
        messagebox.showwarning(
            'Внимание',
            f'Ваша осанка отклонилась на '
            f'{angle - base:+.1f} ° от нормы!')

    def on_close(self):
        self.pose_worker.stop()
        self.root.destroy()

# ───────────────────────────── main ────────────────────────────────
def main():
    root = tk.Tk()
    AppGUI(root)
    root.mainloop()


if __name__ == '__main__':
    main()
