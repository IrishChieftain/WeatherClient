using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;


namespace WeatherClient
{
    public static class Program
    {
        /// <summary>
        /// Some improvements could be made to project structure such as adding model folder, etc,
        /// also some more stringent error handling and logging and possible use of options pattern.
        /// Would also benefit from unit and integration tests.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            Console.Clear();
            Console.WriteLine("WeatherClient needs two parameters, a time interval and a path to log results.");
            Console.WriteLine("Request will be sent up to 3 times using chosen time interval between each request.");
            Console.WriteLine("Results will be written to the log using the path entered.");
            Console.WriteLine("-----------------------------------------------------------------------------------\n");
            Console.WriteLine("Enter a time interval in seconds: 5, 10, or 20.");

            var timeInterval = Console.ReadLine();
            if(!ValidateInterval(timeInterval))
            {
                Reset();
                return;
            }

            Console.WriteLine("Enter a valid directory path for log");
            var directoryPath = Console.ReadLine();
            if(!Directory.Exists(directoryPath))
            {
                Reset();
                return;
            }

            // Apply the time interval input and limit to three requests
            WeatherReport weatherReport;
            using (var timer = new PeriodicTimer(TimeSpan.FromSeconds(Convert.ToInt32(timeInterval))))
            {
                int i = 0;
                while(i < 3)
                {
                    while (await timer.WaitForNextTickAsync())
                    {
                        Console.WriteLine($"Making REST request #{i + 1}");
                        weatherReport = await ProcessReports<WeatherReport>();
                        DateTime dt = UnixDateToDateTime(weatherReport.dt);

                        // Could have parsed JSON directly to file but the DTO mapping here is easier to read/maintain
                        WeatherRecordDTO weatherRecordDTO = new WeatherRecordDTO
                        {
                            TimeStamp = dt,
                            Location = weatherReport.name,
                            CurrentTemp = (decimal)weatherReport.main.temp,
                            Humidity = weatherReport.main.humidity,
                            WindSpeed = (decimal)weatherReport.wind.speed
                        };

                        // Write report to log
                        WriteLog(directoryPath, weatherRecordDTO);

                        i++;
                        break;
                    }
                }
            }
        }

        private static void WriteLog(string docPath, WeatherRecordDTO weatherRecordDTO)
        {
            string path = Path.Combine(docPath, "OpenWeatherData" + "_" + weatherRecordDTO.TimeStamp.ToString("MMddyyyy" + "_" + "HH_mm_ss") + ".txt");
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine("Timestamp: " + weatherRecordDTO.TimeStamp.ToString("F"));
                sw.WriteLine("Location: " + weatherRecordDTO.Location);
                sw.WriteLine("Current Temperature: " + weatherRecordDTO.CurrentTemp);
                sw.WriteLine("Current Humidity: " + weatherRecordDTO.Humidity);
                sw.WriteLine("Current Wind Speed: " + weatherRecordDTO.WindSpeed);
                sw.WriteLine(Environment.NewLine);             
            }
            // NOTE: Logged timestamps are same because the OpenWeatherMap REST results are not being updated as quickly as our time interval (tested this)
        }

        private static void Reset()
        {
            Console.Clear();
            Console.WriteLine("Invalid args");
        }

        private static DateTime UnixDateToDateTime(double unixTimeStamp)
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dateTime;
        }

        private static bool ValidateInterval(string timeInterval)
        {
            if (Int32.TryParse(timeInterval, out int seconds))
            {
                switch (timeInterval)
                {
                    case "5":
                        return true;
                    case "10":
                        return true;
                    case "20":
                        return true;
                    default:
                        return false;
                }
            }
            else
            {
                return false;
            }
        }

        private static async Task<WeatherReport> ProcessReports<WeatherReport>()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://api.openweathermap.org");

                // Needed proper error handling - status codes not dependable with this method!
                //var result = await client.GetStringAsync(client.BaseAddress + $"/data/2.5/weather?q=Parker,US&appid=2b7338ea4ba6e973d5acd266520f34ac&units=imperial");

                var result = await client.GetAsync(client.BaseAddress + $"/data/2.5/weather?q=Parker,US&appid=2b7338ea4ba6e973d5acd266520f34ac&units=imperial");
                result.EnsureSuccessStatusCode();
                var jsonResponse = "";
                if (result.IsSuccessStatusCode)
                    jsonResponse = await result.Content.ReadAsStringAsync();

                WeatherReport weatherReport = JsonConvert.DeserializeObject<WeatherReport>(jsonResponse);
                return weatherReport;
            }
        }
    }
}