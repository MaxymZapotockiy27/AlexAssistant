using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Control;
using Grpc.Core;
using Grpc.Net.Client;

namespace AlexAssistant.Services
{
    public class GrpcService
    {
        private static GrpcService _instance;
        private GrpcChannel _channel;
        public ControlService.ControlServiceClient Client { get; private set; }
        private const int DEFAULT_PORT = 50051;
        private const int MAX_PORT_ATTEMPTS = 10;
        private int _currentPort;

        private GrpcService()
        {
            try
            {
                // Find the correct port to connect to
                _currentPort = FindServerPort();

                var serviceAddress = $"http://localhost:{_currentPort}";

                // Configure the channel to allow unencrypted HTTP2 connection (for development)
                var channelOptions = new GrpcChannelOptions
                {
                    HttpHandler = new System.Net.Http.SocketsHttpHandler
                    {
                        EnableMultipleHttp2Connections = true,
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                        ConnectTimeout = TimeSpan.FromSeconds(10)
                    }
                };

                _channel = GrpcChannel.ForAddress(serviceAddress, channelOptions);
                Client = new ControlService.ControlServiceClient(_channel);

                Console.WriteLine($"gRPC channel created for address: {serviceAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: Failed to create gRPC channel: {ex.Message}");
                throw;
            }
        }

        private int FindServerPort()
        {
            // First try to read from port file
            string portFilePath = Path.Combine(AppContext.BaseDirectory, "PythonBackend", "server_port.txt");

            Console.WriteLine($"Looking for port file at: {portFilePath}");

            if (File.Exists(portFilePath))
            {
                try
                {
                    string portStr = File.ReadAllText(portFilePath).Trim();
                    if (int.TryParse(portStr, out int port))
                    {
                        Console.WriteLine($"Found server port from file: {port}");
                        return port;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading port file: {ex.Message}");
                }
            }

            // If file not found or invalid, try default port range
            Console.WriteLine("Port file not found or invalid, checking if default port is in use...");

            if (IsPortInUse(DEFAULT_PORT))
            {
                Console.WriteLine($"Default port {DEFAULT_PORT} is in use");
                return DEFAULT_PORT;
            }

            // If default port isn't in use, scan for any other potential ports
            Console.WriteLine("Scanning alternative ports...");
            for (int i = 1; i < MAX_PORT_ATTEMPTS; i++)
            {
                int port = DEFAULT_PORT + i;
                if (IsPortInUse(port))
                {
                    Console.WriteLine($"Found potential server port: {port}");
                    return port;
                }
            }

            // If all else fails, return default port
            Console.WriteLine($"No server port detected, using default: {DEFAULT_PORT}");
            return DEFAULT_PORT;
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnections = ipProperties.GetActiveTcpListeners();

                return tcpConnections.Any(endpoint => endpoint.Port == port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking port {port}: {ex.Message}");
                return false;
            }
        }

        public static GrpcService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (typeof(GrpcService))
                    {
                        _instance ??= new GrpcService();
                    }
                }
                return _instance;
            }
        }

        public void DisposeChannel()
        {
            try
            {
                // First set the client to null to prevent new calls
                Client = null;

                // Then dispose the channel with graceful shutdown
                if (_channel != null)
                {
                    // Try to shutdown gracefully first
                    _channel.ShutdownAsync().Wait(TimeSpan.FromSeconds(3));
                    _channel.Dispose();
                    _channel = null;
                    Console.WriteLine("gRPC channel disposed cleanly.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during gRPC channel disposal: {ex.Message}");
            }
        }

        public int CurrentPort => _currentPort;
    }
}