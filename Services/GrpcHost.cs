using Grpc.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace AlexAssistant.Services
{
    public class GrpcHost
    {
        private Server? _server;
        private int _port = 60051;
        private const int CSHARP_SERVER_PORT_MAX = 61000;

        public void Start()
        {
            _port = GetAvailablePort(_port, CSHARP_SERVER_PORT_MAX);

            _server = new Server
            {
                Services = { AssistantBridge.BindService(new AssistantBridgeService()) },
                Ports = { new ServerPort("localhost", _port, ServerCredentials.Insecure) }
            };

            _server.Start();
            Debug.WriteLine($"✅ C# gRPC server (AssistantBridge) started on port {_port}");

          
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string portFile = Path.Combine(baseDir, "servercsharp.txt");

            try
            {
           
                string? dir = Path.GetDirectoryName(portFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

             
                File.WriteAllText(portFile, _port.ToString());
                Debug.WriteLine($"✅ gRPC port written to file: {portFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Failed to write port file: {ex.Message}");
            }
        }

        public async Task Stop()
        {
            if (_server != null)
            {
                await _server.ShutdownAsync();
                Debug.WriteLine("🛑 gRPC server stopped.");
            }
        }

        private int GetAvailablePort(int startPort, int maxPort = 60000)
        {
            var ipProps = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipProps.GetActiveTcpListeners();
            var usedPorts = tcpListeners.Select(p => p.Port).ToHashSet();

            int port = startPort;
            while (usedPorts.Contains(port))
            {
                port++;
                if (port > maxPort)
                    throw new Exception($"❌ No available ports found in range {startPort}-{maxPort}.");
            }
            Debug.WriteLine($"[GrpcHost] Found available port: {port}");
            return port;
        }
    }
}
