using InTheHand.Bluetooth;
using Rug.Osc;
using System.Globalization;

internal class OVRTTconsole
{
    // Calibration routine control
    private static int calibrationStep = 0;
    private static bool calibrationRoutine = false;

    // Devices to which connections are established
    private static BluetoothDevice rightDevice = null;
    private static BluetoothDevice leftDevice = null;

    // OSC sender
    private static OscSender OSCsender = null;

    // Ouput value control
    private static bool outputControl = false;

    // Config file
    private static string configFilePath = "config.conf";

    // Default values for the config file
    private static bool debugOut = false;

    private static string leftTrackerName = "NanoBLE-LEFT";
    private static string leftTrackerServiceUUID = "180c0000-0000-0000-0000-000100010011";
    private static string leftTrackerCharacteristicUUID = "2A56";
    private static string rightTrackerName = "NanoBLE-RIGHT";
    private static string rightTrackerServiceUUID = "180c0000-0000-0000-0000-000100010001";
    private static string rightTrackerCharacteristicUUID = "2A56";

    private static string oscSendIP = "127.0.0.1";
    private static int oscSendPort = 9000;
    private static string oscReceiveIP = "127.0.0.1"; //currently unused as the console does not need any data from OSC/VRC
    private static int oscReceivePort = 9001;         //currently unused as the console does not need any data from OSC/VRC

    private static string leftFootParam = "/avatar/parameters/LeftAllToes";
    private static string rightFootParam = "/avatar/parameters/RightAllToes";

    private static int leftFootFlat = 511;
    private static int leftFootCurl = 0;
    private static int leftFootBend = 1023;

    private static int rightFootFlat = 511;
    private static int rightFootCurl = 0;
    private static int rightFootBend = 1023;

    // Calibration values default to raw values at start
    private static int leftFlatCalibrated = leftFootFlat;
    private static int leftCurlCalibrated = leftFootCurl;
    private static int leftBendCalibrated = leftFootBend;

    private static int rightFlatCalibrated = rightFootFlat;
    private static int rightCurlCalibrated = rightFootCurl;
    private static int rightBendCalibrated = rightFootBend;


    //-------------------------------------------------------

    private static async Task Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Blue;

        Console.WriteLine("Starting the OVRTT console!");

        Console.WriteLine("Checking for config file...");
        ReadConfigFile();

        Console.WriteLine("Looking for devices, this may take more than 30sec...");
        var discoveryTask = BLEDeviceDiscovery();
        discoveryTask.Wait();

        if (discoveryTask.Result)
        {
            Console.WriteLine("------------------------------------------------------");
            Console.WriteLine("The console is now sending data via OSC to the defined parameters.");
            Console.WriteLine("You can press 'c' or spacebar to begin the calibration routine.");
            Console.WriteLine("You can press 'o' to toggle output of read values - WARNING - very spammy!");
            Console.WriteLine("You can press 'e' to exit at any time.");
        }

        Console.WriteLine("------------------------------------------------------");

        // Main loop
        while (discoveryTask.Result)
        {
            // If connection to one of the devices has been lost then exit console
            if (leftDevice == null || rightDevice == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.Black;

                Console.WriteLine("Connection to one or both devices lost.");
                Console.WriteLine("The console will now stop reading and sending data.");
                Console.WriteLine("Please exit and start the console again.");

                ProgramExit();
                break;
            }

            // Handling inputs for calibration and exiting
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                // Calibration control
                if (key.KeyChar == 'c' || key.KeyChar == ' ')
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.BackgroundColor = ConsoleColor.Black;

                    switch (calibrationStep)
                    {
                        case 0:
                            {
                                calibrationRoutine = true;
                                calibrationStep = 1;
                                Console.WriteLine("------------------------------------------------------------------------");
                                Console.WriteLine("You have entered the calibration routine!");
                                Console.WriteLine("The OSC sender is paused until you finish calibration.");
                                Console.WriteLine("Press 'c' or spacebar to calibrate your left foot curl.");
                            }
                            break;
                        case 1:
                            {
                                Console.WriteLine("Press 'c' or spacebar to calibrate your left foot flat.");
                                calibrationStep++;
                            }
                            break;
                        case 2:
                            {
                                Console.WriteLine("Press 'c' or spacebar to calibrate your left foot bend.");
                                calibrationStep++;
                            }
                            break;
                        case 3:
                            {
                                Console.WriteLine("Press 'c' or spacebar to calibrate your right foot curl.");
                                calibrationStep++;
                            }
                            break;
                        case 4:
                            {
                                Console.WriteLine("Press 'c' or spacebar to calibrate your right foot flat.");
                                calibrationStep++;
                            }
                            break;
                        case 5:
                            {
                                Console.WriteLine("Press 'c' or spacebar to calibrate your right foot bend.");
                                calibrationStep++;
                            }
                            break;
                        case 6:
                            {
                                Console.WriteLine("Press 'c' or spacebar to finish calibration routine.");
                                calibrationStep++;
                            }
                            break;
                        case 7:
                            {
                                if (CheckCalibratedValues())
                                {
                                    // If the values are good
                                    Console.WriteLine("All values calibrated!");
                                    Console.WriteLine("Calibration routine is now finished, review the values below:");
                                    Console.WriteLine($"left foot curl: {leftCurlCalibrated} - left foot flat: {leftFlatCalibrated} - left foot bend: {leftBendCalibrated}");
                                    Console.WriteLine($"right foot curl: {rightCurlCalibrated} - right foot flat: {rightFlatCalibrated} - right foot bend: {rightBendCalibrated}");
                                    Console.WriteLine("The OSC sender will now resume sending data.");
                                    Console.WriteLine("You can restart the calibration routine by pressing 'c' or space bar.");
                                    Console.WriteLine("------------------------------------------------------------------------");

                                }
                                else
                                {
                                    // Otherwise revert to default values
                                    Console.WriteLine("WARNING: Values are not calibrated!");
                                    Console.WriteLine("Calibration routine is now finished, however the calibrated values have not been saved.");
                                    Console.WriteLine("Instead, the default values are used, which you can compare with the values you calibrated below:");
                                    Console.WriteLine($"Using; left foot curl: {leftFootCurl} - left foot flat: {leftFootFlat} - left foot bend: {leftFootBend}");
                                    Console.WriteLine($"Badly calibrated; left foot curl: {leftCurlCalibrated} - left foot flat: {leftFlatCalibrated} - left foot bend: {leftBendCalibrated}");
                                    Console.WriteLine($"Using; right foot curl: {rightFootCurl} - right foot flat: {rightFootFlat} - right foot bend: {rightFootBend}");
                                    Console.WriteLine($"Badly calibrated; right foot curl: {rightCurlCalibrated} - right foot flat: {rightFlatCalibrated} - right foot bend: {rightBendCalibrated}");
                                    Console.WriteLine("Make sure that the calibrated values follow this simple formula: curl < flat < bend (or vice versa)");
                                    Console.WriteLine("The OSC sender will now resume sending data.");
                                    Console.WriteLine("You can restart the calibration routine by pressing 'c' or space bar.");
                                    Console.WriteLine("------------------------------------------------------------------------");

                                    leftCurlCalibrated = leftFootCurl;
                                    leftFlatCalibrated = leftFootFlat;
                                    leftBendCalibrated = leftFootBend;

                                    rightCurlCalibrated = rightFootCurl;
                                    rightFlatCalibrated = rightFootFlat;
                                    rightBendCalibrated = rightFootBend;
                                }

                                calibrationStep = 0;
                                calibrationRoutine = false;
                            }
                            break;
                    }

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.BackgroundColor = ConsoleColor.Black;
                }

                // Output control
                if (key.KeyChar == 'o')
                {
                    outputControl = !outputControl;
                    if (!outputControl) Console.WriteLine("You can press 'o' to toggle output of read values - WARNING - very spammy!");
                }

                // Exiting the program
                if (key.KeyChar == 'e')
                {
                    ProgramExit();
                    break;
                }
            }

            await Task.Delay(100); // Delay to prevent high CPU usage
        }

        // Confirm exit from program, this is here so a user can review the values in the console after pressing 'e'
        Console.WriteLine("Press any key to exit...");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                break;
            }
            await Task.Delay(100);
        }

    }

    private static bool CheckCalibratedValues()
    {

        if ((leftBendCalibrated < leftFlatCalibrated && leftFlatCalibrated < leftCurlCalibrated)
                    ||
                (leftBendCalibrated > leftFlatCalibrated && leftFlatCalibrated > leftCurlCalibrated)
            &&
                (rightBendCalibrated < rightFlatCalibrated && rightFlatCalibrated < rightCurlCalibrated)
                    ||
                (rightBendCalibrated > rightFlatCalibrated && rightFlatCalibrated > rightCurlCalibrated))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private static void ProgramExit()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.BackgroundColor = ConsoleColor.Black;

        Console.WriteLine("--------------------------------------------");
        Console.WriteLine("Exiting the OVRTT console.");
        Console.WriteLine("Attempting to disconnect from BLE devices...");

        if (leftDevice != null)
        {
            leftDevice.Gatt.Disconnect();
        }
        if (rightDevice != null)
        {
            rightDevice.Gatt.Disconnect();
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.BackgroundColor = ConsoleColor.Black;

        // Close OSC sender
        if (OSCsender != null)
        {
            // Send default values through OSC
            Console.WriteLine("Sending default values of 0.5 via OSC...");
            Thread.Sleep(100);
            OSCsender.Send(new OscMessage(leftFootParam, 0.5f));
            OSCsender.Send(new OscMessage(rightFootParam, 0.5f));
            Console.WriteLine("Closing the OSC sender...");
            OSCsender.Close();
        }
    }

    //-------------------------------------------------------

    private static async Task<bool> BLEDeviceDiscovery()
    {
        var discoveredDevices = await Bluetooth.ScanForDevicesAsync();

        foreach (var discoveredDevice in discoveredDevices)
        {
            if (discoveredDevice.Name.Equals(rightTrackerName))
            {
                rightDevice = discoveredDevice;
                Console.WriteLine($"Found right target {rightDevice.Name} - {rightDevice.Id} device");
            }
            else if (discoveredDevice.Name.Equals(leftTrackerName))
            {
                leftDevice = discoveredDevice;
                Console.WriteLine($"Found left target {leftDevice.Name} - {leftDevice.Id} device");
            }
            else if (debugOut)
            {
                Console.WriteLine($"Found {discoveredDevice.Name} - {discoveredDevice.Id} device");
            }
        }

        if (rightDevice == null || leftDevice == null)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;

            if (leftDevice == null) Console.WriteLine("Cannot find left target device.");
            if (rightDevice == null) Console.Write("Cannot find right target device."); // Console color fix part 1: using Write instead of WriteLine

            // Console color fix part 2
            Console.ResetColor();
            Console.WriteLine();
            
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine("----------------------------------------");
            Console.WriteLine("Unable to find one or both devices.");
            Console.WriteLine("Please exit and start the console again.");

            return false;
        }

        await Task.WhenAll(ConnectToDevice(rightDevice, rightTrackerServiceUUID), ConnectToDevice(leftDevice, leftTrackerServiceUUID));

        // Set up OSC sender
        try
        {
            Console.WriteLine($"Setting up OSC sender - IP:{oscSendIP}, Port:{oscSendPort}");
            OSCsender = new OscSender(System.Net.IPAddress.Parse(oscSendIP), oscSendPort);
            OSCsender.Connect();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting up OSC sender - IP:{oscSendIP}, Port:{oscSendPort}");
            if (debugOut)
            {
                Console.WriteLine("DEBUG: OSC sender error message below:");
                Console.WriteLine(ex.ToString());
            }
            Console.WriteLine("Please make sure the IP and Port are correct and start the OVRTT console again.");
            return false;
        }

        return true;
    }

    //-------------------------------------------------------

    private static async Task ConnectToDevice(BluetoothDevice device, string serviceGuidString)
    {
        device.GattServerDisconnected += Device_GattServerDisconnected;
        await device.Gatt.ConnectAsync();

        if (device.Gatt.IsConnected)
        {
            Console.WriteLine($"Connected to {device.Name}");
            var serviceGuid = Guid.Parse(serviceGuidString);
            var targetService = await device.Gatt.GetPrimaryServiceAsync(serviceGuid);

            if (targetService != null)
            {
                if (debugOut) Console.WriteLine("Connected to service");

                // Default characteristic value
                ushort shortID = 0x2A56;

                if (device.Name.Equals(leftTrackerName))
                {
                    if (!ushort.TryParse(leftTrackerCharacteristicUUID, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out shortID))
                    {
                        Console.WriteLine($"Failed to parse '{leftTrackerCharacteristicUUID}' to a ushort. Defaulting to {shortID}.");
                    }
                }
                else if (device.Name.Equals(rightTrackerName))
                {
                    if (!ushort.TryParse(rightTrackerCharacteristicUUID, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out shortID))
                    {
                        Console.WriteLine($"Failed to parse '{rightTrackerCharacteristicUUID}' to a ushort. Defaulting to {shortID}.");
                    }
                }

                var targetCharacteristic = await targetService.GetCharacteristicAsync(BluetoothUuid.FromShortId(shortID));
                if (debugOut) Console.WriteLine($"Charactaristic connected to {shortID:X4}");

                if (targetCharacteristic != null)
                {
                    if (debugOut)
                    {
                        Console.WriteLine("Reading value...");
                        var targetValue = await targetCharacteristic.ReadValueAsync();

                        Console.WriteLine($"Value is {BitConverter.ToInt32(targetValue, 0)}");
                    }
                    targetCharacteristic.CharacteristicValueChanged += (sender, args) => Characteristic_CharacteristicValueChanged(sender, args, device.Name);
                    await targetCharacteristic.StartNotificationsAsync();
                }
                else
                {
                    Console.WriteLine("Could not find target characteristic");
                }
            }
            else
            {
                Console.WriteLine("Could not find target service");
            }
        }
        else
        {
            Console.WriteLine($"Could not connect to {device.Name}");
        }
    }

    //-------------------------------------------------------
    private static void Device_GattServerDisconnected(object sender, EventArgs e)
    {
        var device = sender as BluetoothDevice;
        Console.BackgroundColor = ConsoleColor.Red;
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"Device {device.Name} disconnected.");

        if (device.Name.Equals(leftTrackerName)) leftDevice = null;
        if (device.Name.Equals(rightTrackerName)) rightDevice = null;

        // Resetting console color fix
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void Characteristic_CharacteristicValueChanged(object sender, GattCharacteristicValueChangedEventArgs args, string deviceName)
    {
        int value = BitConverter.ToInt32(args.Value, 0);

        // Send data through OSC if not calibrating
        if (!calibrationRoutine && OSCsender != null)
        {
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Blue;


            if (deviceName.Equals(leftDevice.Name))
            {
                // Send data to left foot OSC param
                float floatValue = Normalize(value, leftBendCalibrated, leftFlatCalibrated, leftCurlCalibrated);
                OSCsender.Send(new OscMessage(leftFootParam, floatValue));
                if (outputControl) Console.WriteLine($"Tracker {deviceName}: raw value - {value}, OSC output - {floatValue}");
            }

            if (deviceName.Equals(rightDevice.Name))
            {
                // Send data to right foot OSC param
                float floatValue = Normalize(value, rightBendCalibrated, rightFlatCalibrated, rightCurlCalibrated);
                OSCsender.Send(new OscMessage(rightFootParam, floatValue));
                if (outputControl) Console.WriteLine($"Tracker {deviceName}: raw value - {value}, OSC output - {floatValue}");
            }
        }
        // Assign calibrated values
        else if (calibrationRoutine)
        {
            switch (calibrationStep)
            {
                case 1:
                    {
                        if (deviceName.Equals(leftTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: left foot curl: {value}");
                            leftCurlCalibrated = value;
                        }
                    }
                    break;
                case 2:
                    {
                        if (deviceName.Equals(leftTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: left foot flat: {value}");
                            leftFlatCalibrated = value;
                        }
                    }
                    break;
                case 3:
                    {
                        if (deviceName.Equals(leftTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: left foot bend: {value}");
                            leftBendCalibrated = value;
                        }
                    }
                    break;
                case 4:
                    {
                        if (deviceName.Equals(rightTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: right foot curl: {value}");
                            rightCurlCalibrated = value;
                        }
                    }
                    break;
                case 5:
                    {
                        if (deviceName.Equals(rightTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: right foot flat: {value}");
                            rightFlatCalibrated = value;
                        }
                    }
                    break;
                case 6:
                    {
                        if (deviceName.Equals(rightTrackerName))
                        {
                            Console.WriteLine($"CALIBRATION: right foot bend: {value}");
                            rightBendCalibrated = value;
                        }
                    }
                    break;
            }

        }

    }

    public static float Normalize(int input, int bendCalibrated, int flatCalibrated, int curlCalibrated)
    {
        int minValue = Math.Min(bendCalibrated, curlCalibrated);
        int maxValue = Math.Max(bendCalibrated, curlCalibrated);

        // Check if input value has gone past the calibrated min or max, and if it has, correct the input value
        if (input < minValue) input = minValue;
        if (input > maxValue) input = maxValue;

        // Determine if normalization should be in ascending or descending order
        bool isReversed = bendCalibrated < curlCalibrated;

        // Normalization of input value that is adjusted to the "flatCalibrated" value
        int midpoint = flatCalibrated;
        int distanceToMin = midpoint - minValue;
        int distanceToMax = maxValue - midpoint;

        // Defining midpoint range
        int midpointLow = midpoint - (distanceToMin / 100);
        int midpointHigh = midpoint + (distanceToMax / 100);

        float normalizedValue;

        if (input >= midpointLow && input <= midpointHigh)
        {
            normalizedValue = 50f;
        }
        else if (input < midpointLow)
        {
            // Normalize input to range [minValue, midpointLow] -> [0, 0.50)
            normalizedValue = ((float)(input - minValue) / (midpointLow - minValue)) * 50;
        }
        else
        {
            // Normalize input to range [midpointHigh, maxValue] -> (0.50, 1.00]
            normalizedValue = 50 + ((float)(input - midpointHigh) / (maxValue - midpointHigh)) * 50;
        }

        // Reverse the result if needed and divide by 100 to go from integer to float
        return isReversed ? 1 - normalizedValue / 100 : normalizedValue / 100;
    }
    //-------------------------------------------------------

    private static void ReadConfigFile()
    {

        if (File.Exists(configFilePath))
        {
            Console.WriteLine("Config file found, reading values...");

            var lines = File.ReadAllLines(configFilePath);
            foreach (var line in lines)
            {
                // Remove comments and trim spaces
                var cleanLine = line.Split('*')[0].Trim();
                if (string.IsNullOrWhiteSpace(cleanLine)) continue;

                // Split key and value
                var parts = cleanLine.Split('=');
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                if (debugOut) Console.WriteLine($"DEBUG: config file read {key}: {value}");

                // Set global variables based on key
                switch (key)
                {
                    case "debug_out":
                        debugOut = bool.Parse(value);
                        if (debugOut)
                        {
                            string fullPath = Path.GetFullPath(configFilePath);
                            Console.WriteLine($"DEBUG: config file full path - {fullPath}");
                            Console.WriteLine($"DEBUG: config file read {key}: {value}");
                        }
                        break;
                    case "left_tracker_name":
                        leftTrackerName = value;
                        break;
                    case "left_tracker_service_UUID":
                        leftTrackerServiceUUID = value;
                        break;
                    case "left_tracker_characteristic_UUID":
                        leftTrackerCharacteristicUUID = value;
                        break;
                    case "right_tracker_name":
                        rightTrackerName = value;
                        break;
                    case "right_tracker_service_UUID":
                        rightTrackerServiceUUID = value;
                        break;
                    case "right_tracker_characteristic_UUID":
                        rightTrackerCharacteristicUUID = value;
                        break;
                    case "OSC_send_IP":
                        oscSendIP = value;
                        break;
                    case "OSC_send_port":
                        if (int.TryParse(value, out int sendPort))
                        {
                            oscSendPort = sendPort;
                        }
                        break;
                    case "OSC_receive_IP":
                        oscReceiveIP = value;
                        break;
                    case "OSC_receive_port":
                        if (int.TryParse(value, out int receivePort))
                        {
                            oscReceivePort = receivePort;
                        }
                        break;
                    case "left_foot_param":
                        leftFootParam = value;
                        break;
                    case "right_foot_param":
                        rightFootParam = value;
                        break;
                    case "left_foot_flat":
                        if (int.TryParse(value, out int leftFlat))
                        {
                            leftFootFlat = leftFlat;
                            leftFlatCalibrated = leftFlat;
                        }
                        break;
                    case "left_foot_curl":
                        if (int.TryParse(value, out int leftCurl))
                        {
                            leftFootCurl = leftCurl;
                            leftCurlCalibrated = leftCurl;
                        }
                        break;
                    case "left_foot_bend":
                        if (int.TryParse(value, out int leftBend))
                        {
                            leftFootBend = leftBend;
                            leftBendCalibrated = leftBend;
                        }
                        break;
                    case "right_foot_flat":
                        if (int.TryParse(value, out int rightFlat))
                        {
                            rightFootFlat = rightFlat;
                            rightFlatCalibrated = rightFlat;
                        }
                        break;
                    case "right_foot_curl":
                        if (int.TryParse(value, out int rightCurl))
                        {
                            rightFootCurl = rightCurl;
                            rightCurlCalibrated = rightCurl;
                        }
                        break;
                    case "right_foot_bend":
                        if (int.TryParse(value, out int rightBend))
                        {
                            rightFootBend = rightBend;
                            rightBendCalibrated = rightBend;
                        }
                        break;
                    default:
                        Console.WriteLine($"-WARNING: Unrecognized config file value: '{key}' - '{value}'. It will be not be used!");
                        break;
                }
            }
        }
        else
        {
            Console.WriteLine("Config file NOT found, creating new config.conf file with default values...");

            // Create default config file
            using (StreamWriter writer = new StreamWriter(configFilePath))
            {
                writer.WriteLine("* Comments: everything after a * symbol in a line is ignored");
                writer.WriteLine("* Format: name=value");
                writer.WriteLine("* 'name' - case sensitive and must conform to expected inputs. The default config file contains all possible inputs.");
                writer.WriteLine("* 'value' - only alphanumeric (A-Z, a-z, 0-9) characters allowed, along with these 4 symbols: / . - _");
                writer.WriteLine("* Spaces are ignored.");
                writer.WriteLine("* Empty lines are ignored.");
                writer.WriteLine("");
                writer.WriteLine("* The config file is read line by line, the order of the lines does not matter.");
                writer.WriteLine("* However, if you wish to use \"debug_out\" by setting it to true, it is recommended that it appears first,");
                writer.WriteLine("* above all other values, as is the case in the original config file.");
                writer.WriteLine("");
                writer.WriteLine("* If one of the required values is missing, the OVRTT console will revert to a built in default value,");
                writer.WriteLine("* however this missing default value will not be written to the config file.");
                writer.WriteLine("");
                writer.WriteLine("* The default values in the OVRTT console can be viewed in the program source code, or you can reset the");
                writer.WriteLine("* config file with the console.");
                writer.WriteLine("");
                writer.WriteLine("* To reset the config file, delete (or rename) the config file from the folder where the OVRTTconsole.exe is located,");
                writer.WriteLine("* and then run the console, this should create a new config.conf file with all the required default values filled out.");
                writer.WriteLine("");
                writer.WriteLine("* Alternatively, you can re-download the release from the GitHub page, and extract the config file to overwrite the");
                writer.WriteLine("* one that is located in the same folder as the OVRTT console executable.");
                writer.WriteLine("");
                writer.WriteLine("* The config file is read only once, when the OVRTT console is started.");
                writer.WriteLine("* While you can edit the config file when the console is running, it is not recommended to do so.");
                writer.WriteLine("* Exit the console, edit and save the config file, then start the console again.");
                writer.WriteLine("* If you have edited the config file while the console is running,");
                writer.WriteLine("* the console needs to be restarted to read the newly edited config file.");

                writer.WriteLine("");

                writer.WriteLine("*************************");
                writer.WriteLine("*                       *");
                writer.WriteLine("*     DEBUG  THINGS     *");
                writer.WriteLine("*                       *");
                writer.WriteLine("*************************");
                writer.WriteLine("* If set to true, there will be extra \"DEBUG\" outputs in the OVRTT console");
                writer.WriteLine("debug_out=" + debugOut);

                writer.WriteLine("");

                writer.WriteLine("*************************");
                writer.WriteLine("*                       *");
                writer.WriteLine("*     BLE  SETTINGS     *");
                writer.WriteLine("*                       *");
                writer.WriteLine("*************************");
                writer.WriteLine("* UUID of the left tracker service");
                writer.WriteLine("left_tracker_name=" + leftTrackerName);
                writer.WriteLine("* UUID of the left tracker service");
                writer.WriteLine("left_tracker_service_UUID=" + leftTrackerServiceUUID);
                writer.WriteLine("* UUID of the left tracker characteristic");
                writer.WriteLine("left_tracker_characteristic_UUID=" + leftTrackerCharacteristicUUID);

                writer.WriteLine("");

                writer.WriteLine("* UUID of the right tracker service");
                writer.WriteLine("right_tracker_name=" + rightTrackerName);
                writer.WriteLine("* UUID of the right tracker service");
                writer.WriteLine("right_tracker_service_UUID=" + rightTrackerServiceUUID);
                writer.WriteLine("* UUID of the right tracker characteristic");
                writer.WriteLine("right_tracker_characteristic_UUID=" + rightTrackerCharacteristicUUID);

                writer.WriteLine("");

                writer.WriteLine("*************************");
                writer.WriteLine("*                       *");
                writer.WriteLine("*     OSC  SETTINGS     *");
                writer.WriteLine("*                       *");
                writer.WriteLine("*************************");
                writer.WriteLine("* IP and port to send data to");
                writer.WriteLine("OSC_send_IP=" + oscSendIP);
                writer.WriteLine("OSC_send_port=" + oscSendPort);

                writer.WriteLine("");

                writer.WriteLine("* IP and port to receive data from");
                writer.WriteLine("OSC_receive_IP=" + oscReceiveIP);
                writer.WriteLine("OSC_receive_port=" + oscReceivePort);

                writer.WriteLine("");

                writer.WriteLine("* Parameters to send OSC data to");
                writer.WriteLine("left_foot_param=" + leftFootParam);
                writer.WriteLine("right_foot_param=" + rightFootParam);

                writer.WriteLine("");

                writer.WriteLine("*************************");
                writer.WriteLine("*                       *");
                writer.WriteLine("*  DATA & CALIBRATION   *");
                writer.WriteLine("*                       *");
                writer.WriteLine("*************************");
                writer.WriteLine("* These are the default values that the tracker will send");
                writer.WriteLine("left_foot_flat=" + leftFootFlat);
                writer.WriteLine("left_foot_curl=" + leftFootCurl);
                writer.WriteLine("left_foot_bend=" + leftFootBend);

                writer.WriteLine("");

                writer.WriteLine("right_foot_flat=" + rightFootFlat);
                writer.WriteLine("right_foot_curl=" + rightFootCurl);
                writer.WriteLine("right_foot_bend=" + rightFootBend);
            }
        }
    }

}