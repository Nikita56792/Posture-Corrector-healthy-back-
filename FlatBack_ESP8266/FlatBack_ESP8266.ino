#include <ESP8266WiFi.h>
#include <WiFiUdp.h>
#include <EEPROM.h>
#include <ESP8266WebServer.h>
#include <Adafruit_MPU6050.h>
#include <Adafruit_Sensor.h>
#include <Wire.h>
#include <Adafruit_BMP280.h>

Adafruit_MPU6050 mpu;
Adafruit_BMP280 bmp280;
float inc = 0;
int calibrt = 4.5;
float gyroZ;

#define EEPROM_SIZE 512
#define SSID_LEN 32
#define PASS_LEN 64
#define SSID_ADDR 0
#define PASS_ADDR (SSID_ADDR + SSID_LEN + 1)  // +1 для нулевого символа конца строки
const int localPort = 1234;
const char* vendorId = "123985";
WiFiUDP udp;
ESP8266WebServer server(80);
bool gdsr = true;

void setup() {
  Serial.begin(115200);
  pinMode(15, OUTPUT);
  delay(10);
  if (!mpu.begin()) {
    while (1) {
      delay(10);
    }
    mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
    Serial.print("Accelerometer range set to: ");
    mpu.setGyroRange(MPU6050_RANGE_500_DEG);
    Serial.print("Gyro range set to: ");

    mpu.setFilterBandwidth(MPU6050_BAND_21_HZ);
    delay(100);
  }
  delay(100);
  EEPROM.begin(EEPROM_SIZE);
  WiFi.mode(WIFI_STA);
  if (!tryConnectWiFi()) {
    setupAccessPoint();
  } else {
    udp.begin(localPort);
  }
}

bool tryConnectWiFi() {
  String ssid = readStringFromEEPROM(SSID_ADDR, SSID_LEN);
  String password = readStringFromEEPROM(PASS_ADDR, PASS_LEN);
  WiFi.begin(ssid.c_str(), password.c_str());

  unsigned long startTime = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - startTime < 10000) {
    delay(500);
    Serial.print(".");
  }

  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("\nFailed to connect to WiFi. Starting access point...");
    return false;
  } else {
    Serial.println("\nConnected to WiFi");
    return true;
  }
}

const char* htmlTemplate =
  "<!DOCTYPE html>"
  "<html lang='ru'>"
  "<head>"
  "<meta charset='UTF-8'>"
  "<meta name='viewport' content='width=device-width, initial-scale=1.0'>"
  "<title>Настройка Wi-Fi</title>"
  "<style>"
  "body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f0f0f0; }"
  "h1 { color: #333; }"
  "form { margin-top: 20px; }"
  "input[type='text'], input[type='password'], input[type='submit'] { width: 100%; padding: 10px; margin: 10px 0; border: 1px solid #ddd; border-radius: 5px; box-sizing: border-box; }"
  "input[type='submit'] { background-color: #4CAF50; color: white; }"
  "input[type='submit']:hover { opacity: 0.8; }"
  ".warning { color: red; margin-top: 20px; }"
  "</style>"
  "</head>"
  "<body>"
  "<h1>Настройка подключения к Wi-Fi</h1>"
  "<form action='/save' method='POST'>"
  "Имя сети (SSID): <br><input type='text' name='ssid'><br>"
  "Пароль: <br><input type='password' name='pass'><br>"
  "<input type='submit' value='Сохранить'>"
  "</form>"
  "<form action='/reboot_without_save' method='GET'>"
  "<input type='submit' value='Перезагрузить без сохранения'>"
  "</form>"
  "<div class='warning'>Вводите текст только латинскими символами.</div>"
  "</body>"
  "</html>";


void setupAccessPoint() {
  const char* apSSID = "FlatBack_WiFi";
  WiFi.mode(WIFI_AP);
  WiFi.softAP(apSSID);
  server.on("/", HTTP_GET, []() {
    server.send(200, "text/html", htmlTemplate);
  });

  server.on("/save", HTTP_POST, []() {
    String ssid = server.arg("ssid");
    String pass = server.arg("pass");
    writeStringToEEPROM(SSID_ADDR, ssid, SSID_LEN);
    writeStringToEEPROM(PASS_ADDR, pass, PASS_LEN);
    EEPROM.commit();

    const char* savedPageTemplate =
      "<!DOCTYPE html>"
      "<html lang='ru'>"
      "<head>"
      "<meta charset='UTF-8'>"
      "<meta name='viewport' content='width=device-width, initial-scale=1.0'>"
      "<title>Настройки сохранены</title>"
      "<meta http-equiv='refresh' content='10;url=/' />"  // Переход на главную страницу через 10 секунд после перезагрузки
      "<style>"
      "body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f0f0f0; }"
      "h1 { color: #333; }"
      "</style>"
      "</head>"
      "<body>"
      "<h1>Настройки сохранены. Вы можете вернуться в приложения. Устройство перезагружается...</h1>"
      "</body>"
      "</html>";

    server.send(200, "text/html", savedPageTemplate);
    delay(1000);  // Дать время на отправку ответа
    ESP.restart();
  });


  server.on("/reboot", HTTP_GET, []() {
    server.send(200, "text/html", "<h1>Rebooting now...</h1>");
    delay(1000);  // Give time for the response to be sent
    server.send(200, "text/html", htmlTemplate);
    ESP.restart();
  });

  server.on("/reboot_without_save", HTTP_GET, []() {
    const char* rebootPageTemplate =
      "<!DOCTYPE html>"
      "<html lang='ru'>"
      "<head>"
      "<meta charset='UTF-8'>"
      "<meta name='viewport' content='width=device-width, initial-scale=1.0'>"
      "<title>Перезагрузка</title>"
      "<meta http-equiv='refresh' content='10;url=/' />"  // Переход на главную страницу через 10 секунд
      "<style>"
      "body { font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f0f0f0; }"
      "h1 { color: #333; }"
      "</style>"
      "</head>"
      "<body>"
      "<h1>Перезагрузка устройства. Вы можете вернуться в приложения. Пожалуйста, подождите...</h1>"
      "</body>"
      "</html>";

    server.send(200, "text/html", rebootPageTemplate);
    delay(1000);  // Дать время на отправку ответа
    ESP.restart();
  });


  server.begin();
  Serial.println("Access Point Started");
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    if (gdsr) {
      setupAccessPoint();
      udp.begin(localPort);
      gdsr = false;
    }
    // Обработка UDP пакетов
    udpTreatment();
    server.handleClient();
    angleMesaure();
  } else {
    // Обработка UDP пакетов
    udpTreatment();
    angleMesaure();
    gdsr = true;
  }
}
void angleMesaure() {
  float temperature = bmp280.readTemperature();
  float pressure = bmp280.readPressure();
  float altitude = bmp280.readAltitude(1013.25);
  sensors_event_t a, g, temp;
  mpu.getEvent(&a, &g, &temp);
  gyroZ = a.acceleration.z;
  Serial.println(gyroZ);
  if (gyroZ < (calibrt - (calibrt * 2)) or gyroZ > calibrt) {
    digitalWrite(15, HIGH);
    delay(200);
    digitalWrite(15, LOW);
    delay(40);
  } else {
    digitalWrite(15, LOW);
  }
  delay(10);
}

void udpTreatment() {  // Обработка UDP пакетов
  int packetSize = udp.parsePacket();
  if (packetSize) {
    char packetBuffer[255];  // Буфер для хранения входящих данных
    int len = udp.read(packetBuffer, 255);
    if (len > 0) {
      packetBuffer[len] = 0;  // Добавляем нулевой символ в конец, чтобы получить строку
    }
    Serial.printf("Received %d bytes from %s, port %d\n", len, udp.remoteIP().toString().c_str(), udp.remotePort());
    Serial.printf("UDP packet contents: %s\n", packetBuffer);

    // Проверка содержимого пакета и отправка ответа
    if (atoi(packetBuffer) == 242) {
      udp.beginPacket(udp.remoteIP(), udp.remotePort());
      udp.write(vendorId);
      udp.endPacket();
    }
    if (atoi(packetBuffer) == 567) {
      angleMesaure();
      calibrt = gyroZ + 1;
    }
  }
}

void writeStringToEEPROM(int start, String data, int maxLen) {
  int dataSize = data.length();
  if (dataSize > maxLen) dataSize = maxLen;
  for (int i = 0; i < dataSize; ++i) {
    EEPROM.write(start + i, data[i]);
  }
  EEPROM.write(start + dataSize, 0);  // Нулевой символ для обозначения конца строки
  EEPROM.commit();
}

String readStringFromEEPROM(int start, int maxLen) {
  char data[maxLen + 1];  // +1 для нулевого символа
  for (int i = 0; i < maxLen; ++i) {
    data[i] = EEPROM.read(start + i);
    if (data[i] == 0) {
      break;  // Найден конец строки
    }
  }
  data[maxLen] = 0;  // На всякий случай
  return String(data);
}
