#include <ArduinoBLE.h>

const int analogPin = A4; // Must equal to pin that the washer from the Linear sensor is soldered to
int LED_timer = 0;

BLEService sensorService("180C0000-0000-0000-0000-000100010001"); // Create a BLE service

BLEUnsignedIntCharacteristic sensorCharacteristic("2A56", BLERead | BLENotify); // Create a BLE characteristic

void setup() {
  Serial.begin(9600);

  // Turn off sensors and PWR LED
  digitalWrite(PIN_ENABLE_SENSORS_3V3, LOW);
  digitalWrite(PIN_ENABLE_I2C_PULLUP, LOW);
  digitalWrite(LED_PWR, LOW);

  // Initialize BLE
  if (!BLE.begin()) {
    Serial.println("Starting BLE failed!");
    while (1);
  }

  // Set the local and device name for the BLE device
  BLE.setLocalName("NanoBLE-LEFT");
  BLE.setDeviceName("NanoBLE-LEFT");

  // Add the service and characteristic  
  sensorService.addCharacteristic(sensorCharacteristic);
  BLE.addService(sensorService);
  
  // Set initial value for the characteristic
  sensorCharacteristic.writeValue(0);

  // Start advertising
  BLE.advertise();
}

void loop() {
	
  // Read analog value
  int sensorValue = analogRead(analogPin);

  // LED blink control
  if (LED_timer < 300){
    digitalWrite(LEDG, LOW);
  } else if (LED_timer < 5000){
    digitalWrite(LEDG, HIGH);
  } else {
    LED_timer = 0;
  }

  LED_timer = LED_timer + 100;
  
  // Optional output for serial monitor with Arduino IDE
  // Convert the sensor value to a string
  //String sensorData = String(sensorValue);
  //Serial.println("DATA: "+sensorData);
  //Serial.println("Address: "+BLE.address());
  //Serial.println("CarUUID: "+String(sensorCharacteristic.uuid()));
  //Serial.println("SerUUID: "+String(sensorService.uuid()));

  // Update the BLE characteristic
  sensorCharacteristic.writeValue(sensorValue);

  delay(100);
}
