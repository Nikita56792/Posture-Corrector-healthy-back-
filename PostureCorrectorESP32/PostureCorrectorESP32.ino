#include <WiFi.h>
#include <WiFiUdp.h>
#include <WebServer.h>
#include <EEPROM.h>

#include <Wire.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <Adafruit_BMP280.h>

/* ==================== аппарат ==================== */
#define PIN_VIBRO       17          
#define I2C_SDA         21
#define I2C_SCL         22

using static UnityEditor.Rendering.CameraUI;
using System.Security.Claims;
using System;
using Unity.Android.Gradle.Manifest;
using UnityEngine;

Adafruit_MPU6050 mpu;
Adafruit_BMP280 bmp;

/* ==================== сеть ======================= */
const char* VENDOR_ID = "123985";
const uint16_t UDP_PORT = 1234;

WiFiUDP udp;
WebServer server(80);

/* ------------ Wi-Fi ----------------------------- */
const char* WIFI_SSID_DEF = "FlatBack_WiFi";
const char* WIFI_PASS_DEF = "";

/* EEPROM:  SSID/PASS и калибровка */
#define EEPROM_SIZE      1024
#define EE_SSID          0
#define EE_PASS          64
#define EE_CALIB         192     // 16 B

String readString(int addr, int max);
void writeString(int addr, String s, int max);

/* ================== TinyML ======================= */
#include "tensorflow/lite/micro/all_ops_resolver.h"
#include "tensorflow/lite/micro/micro_interpreter.h"
#include "tensorflow/lite/schema/schema_generated.h"
#include "model_data.h"                     // <-- ваш .h с моделью

constexpr int TENSOR_ARENA = 12*1024;
uint8_t tensor_arena[TENSOR_ARENA];

const tflite::Model* model;
tflite::MicroInterpreter* interp;
TfLiteTensor* inT;
TfLiteTensor* outT;

struct Frame { float ax, ay, az, gx, gy, gz, alt; };
constexpr int WIN=64; Frame buf[WIN]; int wp = 0, filled = 0;
constexpr int STEP_MS=20; uint32_t lastMs = 0;

/* скользящее голосование */
int badWin[5] = { 0 }, bIdx = 0, badSum = 0;
const int BAD_LIMIT = 2;

/* ================== калибровка =================== */
struct Calib { float ax0, ay0, az0; }
calib = { 0,0,1}
;
void saveCalib() { EEPROM.put(EE_CALIB, calib); EEPROM.commit(); }
void loadCalib() { EEPROM.get(EE_CALIB, calib); }

/* ================================================= */
void setup()
{
    pinMode(PIN_VIBRO, OUTPUT);
    Serial.begin(115200);

    Wire.begin(I2C_SDA, I2C_SCL);
    if (!mpu.begin()) while (1) ;
    mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
    mpu.setGyroRange(MPU6050_RANGE_500_DEG);
    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
    bmp.begin(0x76);

    /* TinyML init */
    model = tflite::GetModel(g_model_data);
    static tflite::AllOpsResolver resolver;
    static tflite::MicroInterpreter staticInterp(model,&resolver,
                              tensor_arena,TENSOR_ARENA,nullptr);
    interp = &staticInterp; interp->AllocateTensors();
    inT = interp->input(0); outT = interp->output(0);

    /* EEPROM & калибровка */
    EEPROM.begin(EEPROM_SIZE); loadCalib();

    /* Wi-Fi STA попытка */
    WiFi.mode(WIFI_STA);
    String ssid = readString(EE_SSID, 32);
    String pass = readString(EE_PASS, 64);
    if (ssid.length())
    {
        WiFi.begin(ssid.c_str(), pass.c_str());
        if (WiFi.waitForConnectResult() == WL_CONNECTED)
        {
            udp.begin(UDP_PORT);
            Serial.println("STA connected: " + WiFi.localIP().toString());
        }
    }
    if (WiFi.status() != WL_CONNECTED)
    {                       
        WiFi.mode(WIFI_AP);
        WiFi.softAP(WIFI_SSID_DEF, WIFI_PASS_DEF);
        Serial.println("AP: FlatBack_WiFi  192.168.4.1");
    }

    server.on("/", HTTP_GET, handleRoot);
    server.on("/save", HTTP_POST, handleSave);
    server.on("/calib", HTTP_GET, handleCalib);
    server.begin();
}

/* ==================== loop ====================== */
void loop()
{
    readSensorsAndInfer();
    udpProcess();
    server.handleClient();
}

/* -------------- сбор + инференс ----------------- */
void readSensorsAndInfer()
{
    if (millis() - lastMs < STEP_MS) return;
    lastMs = millis();

    sensors_event_t a, g, t;
    mpu.getEvent(&a, &g, &t);
    float alt = bmp.readAltitude(1013.25);

    buf[wp] = {
        a.acceleration.x - calib.ax0,
             a.acceleration.y - calib.ay0,
             a.acceleration.z - calib.az0,
             g.gyro.x, g.gyro.y, g.gyro.z, alt}
    ;
    wp = (wp + 1) % WIN; if (filled < WIN) filled++; else;

    if (filled < WIN) return;

    float feat[18];
    featExtract(feat);

    for (int i = 0; i < 18; i++)
    {
        float z = (feat[i] - featMean[i]) * featInvStd[i];
        inT->data.int8[i] = (int8_t)roundf(z * 64);
    }
    interp->Invoke();
    bool bad = outT->data.int8[1] > 0;

    badSum += -badWin[bIdx] + bad;
    badWin[bIdx] = bad; bIdx = (bIdx + 1) % 5;

    if (badSum >= BAD_LIMIT) digitalWrite(PIN_VIBRO, HIGH);
    else digitalWrite(PIN_VIBRO, LOW);
}

void featExtract(float* f)
{
    float mx = 0, my = 0, mz = 0;
    for (int i = 0; i < WIN; i++) { mx += buf[i].ax; my += buf[i].ay; mz += buf[i].az; }
    mx /= WIN; my /= WIN; mz /= WIN;
    f[0] = mx; f[1] = my; f[2] = mz;
}

/* ---------------- UDP ---------------- */
void udpProcess()
{
    int sz = udp.parsePacket(); if (!sz) return;
    char b[32]; sz = udp.read(b, 31); b[sz] = 0;

    if (!strcmp(b, "242"))            // DISCOVER
    {
        udp.beginPacket(udp.remoteIP(), udp.remotePort());
        udp.write(VENDOR_ID); udp.endPacket();
    }
    else if (!strcmp(b, "567"))       // CALIBRATE
    {
        sensors_event_t a, g, t; mpu.getEvent(&a, &g, &t);
        calib.ax0 = a.acceleration.x; calib.ay0 = a.acceleration.y;
        calib.az0 = a.acceleration.z;
        saveCalib();
    }
}

/* ------------- Web-страницы -------------- */
const char HTML[] PROGMEM =
  "<!DOCTYPE html><html><head><meta charset='utf-8'/>"
  "<title>Wi-Fi setup</title></head><body>"
  "<h2>Wi-Fi</h2><form method='POST' action='/save'>"
  "SSID:<input name='s'><br>Password:<input name='p' type='password'><br>"
  "<input type='submit'></form><hr>"
  "<a href='/calib'>Калибровать позу</a></body></html>";

void handleRoot() { server.send(200, "text/html", HTML); }
void handleSave()
{
    writeString(EE_SSID, server.arg("s"), 32);
    writeString(EE_PASS, server.arg("p"), 64);
    server.send(200, "text/html", "Saved. Reboot…");
    delay(500); ESP.restart();
}
void handleCalib()
{
    sensors_event_t a, g, t; mpu.getEvent(&a, &g, &t);
    calib.ax0 = a.acceleration.x; calib.ay0 = a.acceleration.y;
    calib.az0 = a.acceleration.z; saveCalib();
    server.send(200, "text/plain", "Calibrated!");
}

/* -------- EEPROM helpers -------- */
String readString(int addr, int max)
{
    char s[max + 1]; for (int i = 0; i < max; i++) { s[i] = EEPROM.read(addr + i); if (!s[i]) break; }
    s[max] = 0; return String(s);
}
void writeString(int addr, String s, int max)
{
    int n = s.length(); if (n > max) n = max;
    for (int i = 0; i < n; i++) EEPROM.write(addr + i, s[i]);
    EEPROM.write(addr + n, 0); EEPROM.commit();
}
