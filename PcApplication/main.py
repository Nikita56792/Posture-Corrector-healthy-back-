import cv2
import mediapipe as mp
import math
from tkinter import messagebox
import tkinter as tk

mp_drawing = mp.solutions.drawing_utils
mp_pose = mp.solutions.pose

def calculate_angle(a, b, c):
    angle_rad = math.atan2(c[1] - b[1], c[0] - b[0]) - math.atan2(a[1] - b[1], a[0] - b[0])
    angle_rad = abs(angle_rad)
    angle_deg = math.degrees(angle_rad)
    return angle_deg

class PoseDetector:
    def __init__(self):
        self.cap = cv2.VideoCapture(0)
        self.notification_sent = False
        self.calibrated_angle = None

        self.root = tk.Tk()
        self.root.title("Spine Pose Detector")

        self.calibration_button = tk.Button(self.root, text="Калибровка", command=self.calibrate)
        self.calibration_button.pack(pady=10)

        self.recalibration_button = tk.Button(self.root, text="Перекалибровка", command=self.recalibrate)
        self.recalibration_button.pack(pady=10)

        self.label = tk.Label(self.root, text="Угол наклона спины:")
        self.label.pack()

        self.pose = mp_pose.Pose(min_detection_confidence=0.5, min_tracking_confidence=0.5)

        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

        self.update_gui()

    def calibrate(self):
        if self.calibrated_angle is not None:
            messagebox.showinfo("Калибровка", "Калибровка уже выполнена. Угол: {:.2f} градусов".format(self.calibrated_angle))
        else:
            ret, frame = self.cap.read()
            if not ret:
                messagebox.showerror("Ошибка", "Не удалось получить кадр с веб-камеры.")
                return

            image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = self.pose.process(image_rgb)

            if results.pose_landmarks:
                landmarks = results.pose_landmarks.landmark
                shoulder = (landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER.value].x,
                            landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER.value].y)
                hip = (landmarks[mp_pose.PoseLandmark.LEFT_HIP.value].x,
                       landmarks[mp_pose.PoseLandmark.LEFT_HIP.value].y)
                knee = (landmarks[mp_pose.PoseLandmark.LEFT_KNEE.value].x,
                        landmarks[mp_pose.PoseLandmark.LEFT_KNEE.value].y)

                angle = calculate_angle(shoulder, hip, knee)
                self.calibrated_angle = angle
                messagebox.showinfo("Калибровка", "Калибровка выполнена. Угол: {:.2f} градусов".format(angle))

                # Вывод координат ключевых точек
                messagebox.showinfo("Положение тела", f"Плечо: {shoulder}, Бедро: {hip}, Колено: {knee}")

    def recalibrate(self):
        self.calibrated_angle = None
        messagebox.showinfo("Перекалибровка", "Калибровка сброшена. Выполните новую калибровку.")

    def update_gui(self):
        ret, frame = self.cap.read()
        if not ret:
            self.root.after(100, self.update_gui)
            return

        image_rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        results = self.pose.process(image_rgb)

        if results.pose_landmarks:
            landmarks = results.pose_landmarks.landmark
            shoulder = (landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER.value].x,
                        landmarks[mp_pose.PoseLandmark.LEFT_SHOULDER.value].y)
            hip = (landmarks[mp_pose.PoseLandmark.LEFT_HIP.value].x,
                   landmarks[mp_pose.PoseLandmark.LEFT_HIP.value].y)
            knee = (landmarks[mp_pose.PoseLandmark.LEFT_KNEE.value].x,
                    landmarks[mp_pose.PoseLandmark.LEFT_KNEE.value].y)

            angle = calculate_angle(shoulder, hip, knee)
            print(angle)
            cv2.putText(frame, f'Angle: {int(angle)} degrees', (50, 50),
                        cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2, cv2.LINE_AA)

            if self.calibrated_angle is not None and (angle > self.calibrated_angle + 0.7 or angle < self.calibrated_angle - 0.7):
                if not self.notification_sent:
                    self.send_notification("Ваша осанка не оптимальна!")
                    self.notification_sent = True
                    print("bad")
            else:
                self.notification_sent = False
                print("good")

            mp_drawing.draw_landmarks(frame, results.pose_landmarks, mp_pose.POSE_CONNECTIONS)

            self.label.config(text="Угол наклона спины: {:.2f} градусов".format(angle))
            cv2.imshow('Pose Detection', frame)
        self.root.after(100, self.update_gui)

    def send_notification(self, message):
        messagebox.showwarning("Spine Alert", message)

    def on_close(self):
        self.cap.release()
        self.root.destroy()

if __name__ == "__main__":
    detector = PoseDetector()
    detector.root.mainloop()
