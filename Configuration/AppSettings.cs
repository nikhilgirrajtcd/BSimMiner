using Microsoft.Extensions.Configuration;

namespace BSimClient.Configuration
{
    public class AppSettings
    {
        private static AppSettings _instance = null;
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppSettings();
                    LoadSettings(_instance);
                }
                return _instance;
            }
        }
        private AppSettings() { }
        public string ServiceUrl { get; set; }

        private static void LoadSettings(AppSettings loadInto)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            ConfigurationBinder.Bind(config, loadInto);
        }
    }
}
