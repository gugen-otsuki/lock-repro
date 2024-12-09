using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

namespace IoTHubMessageSender;

internal class Program
{
    private static readonly TimeSpan MessageInterval = TimeSpan.FromSeconds(1); // Hardcoded interval

    private static async Task<int> Main(string[] args)
    {
        DeviceSimulator
        // Retrieve the connection string from environment variable
        string deviceConnectionString = Environment.GetEnvironmentVariable("IOTHUB_DEVICE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(deviceConnectionString))
        {
            Console.WriteLine("Error: Environment variable 'IOTHUB_DEVICE_CONNECTION_STRING' is not set or is empty.");
            return 1;
        }

        using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
            Console.WriteLine("Exiting...");
        };

        var sendTask = SendTimestampMessagesToIoTHubAsync(deviceClient, cts.Token);
        var receiveTask = ReceiveMessagesFromIoTHubAsync(deviceClient, cts.Token);
        await Task.WhenAll(sendTask, receiveTask);

        await deviceClient.CloseAsync();
        Console.WriteLine("Message sender finished.");
        return 0;
    }

    private static async Task SendTimestampMessagesToIoTHubAsync(DeviceClient deviceClient, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Create a message containing the current timestamp
                var messageBody = JsonSerializer.Serialize(new
                {
                    timestamp = DateTime.UtcNow.ToString("o") // ISO 8601 format
                });

                using var message = new Message(Encoding.UTF8.GetBytes(messageBody))
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8",
                };

                await deviceClient.SendEventAsync(message, ct);
                Console.WriteLine($"{DateTime.Now} > Sent message: {messageBody}");

                await Task.Delay(MessageInterval, ct);
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Message sending canceled.");
        }
    }


    private static async Task ReceiveMessagesFromIoTHubAsync(DeviceClient deviceClient, CancellationToken ct)
    {
        Console.WriteLine("Listening for incoming messages...");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var receivedMessage = await deviceClient.ReceiveAsync(TimeSpan.FromSeconds(1));
                if (receivedMessage == null)
                {
                    continue;
                }

                string messageBody = Encoding.UTF8.GetString(receivedMessage.GetBytes());
                Console.WriteLine($"{DateTime.Now} > Received message: {messageBody}");

                // Acknowledge the message
                await deviceClient.CompleteAsync(receivedMessage, ct);
            }
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Message receiving canceled.");
        }
    }
}

