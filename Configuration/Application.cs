using Grpc.Net.Client;

using BSimClient.CommandLine;

namespace BSimClient.Configuration
{
    public class Application
    {
        public static AppSettings Settings => AppSettings.Instance;
        public static MinerOptions StartupParameters { get; set; }

        private static GrpcChannel _channel;

        public static GrpcChannel Channel
        {
            get
            {
                if (_channel == null)
                    _channel = GrpcChannel.ForAddress(Settings.ServiceUrl);
                return _channel;
            }
        }
    }
}
