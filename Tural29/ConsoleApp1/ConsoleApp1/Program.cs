using System.Globalization;
using System.Net;
using System.Text.Json;

namespace WeatherInformationSystem;

public class Program
{
    private static readonly HttpClient Client = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static async Task Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== Weather Information System ===");
        Console.WriteLine("Çıxmaq üçün 'exit' yazın.\n");

        while (true)
        {
            Console.Write("Şəhər adı: ");
            string? input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
            {
                Console.WriteLine("Şəhər adı boş ola bilməz.\n");
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Proqram bağlandı.");
                break;
            }

            try
            {
                await ShowWeatherAsync(input);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Şəbəkə xətası: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Sorğunun vaxtı bitdi. Yenidən cəhd edin.");
            }
            catch (JsonException)
            {
                Console.WriteLine("API cavabını oxumaq mümkün olmadı.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gözlənilməz xəta: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static async Task ShowWeatherAsync(string city)
    {
        string geoUrl = "https://geocoding-api.open-meteo.com/v1/search" +
                        $"?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";

        string? geoJson = await GetJsonAsync(geoUrl);
        if (geoJson == null)
        {
            return;
        }

        using JsonDocument geoDoc = JsonDocument.Parse(geoJson);

        if (!geoDoc.RootElement.TryGetProperty("results", out JsonElement results) ||
            results.GetArrayLength() == 0)
        {
            Console.WriteLine($"'{city}' adlı şəhər tapılmadı.");
            return;
        }

        JsonElement place = results[0];
        string name = place.GetProperty("name").GetString() ?? city;
        string country = place.TryGetProperty("country", out JsonElement c) ? c.GetString() ?? "" : "";
        double lat = place.GetProperty("latitude").GetDouble();
        double lon = place.GetProperty("longitude").GetDouble();

        string weatherUrl = "https://api.open-meteo.com/v1/forecast" +
                            $"?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                            $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m" +
                            "&wind_speed_unit=kmh";

        string? weatherJson = await GetJsonAsync(weatherUrl);
        if (weatherJson == null)
        {
            return;
        }

        using JsonDocument weatherDoc = JsonDocument.Parse(weatherJson);
        JsonElement current = weatherDoc.RootElement.GetProperty("current");

        double temperature = current.GetProperty("temperature_2m").GetDouble();
        double feelsLike = current.GetProperty("apparent_temperature").GetDouble();
        int humidity = current.GetProperty("relative_humidity_2m").GetInt32();
        double windSpeed = current.GetProperty("wind_speed_10m").GetDouble();
        int weatherCode = current.GetProperty("weather_code").GetInt32();

        Console.WriteLine();
        Console.WriteLine("----------------------------------");
        Console.WriteLine($"Şəhər           : {name}{(string.IsNullOrEmpty(country) ? "" : ", " + country)}");
        Console.WriteLine($"Temperatur      : {temperature.ToString("0.0", CultureInfo.InvariantCulture)} °C");
        Console.WriteLine($"Hiss edilən     : {feelsLike.ToString("0.0", CultureInfo.InvariantCulture)} °C");
        Console.WriteLine($"Hava şəraiti    : {DescribeWeather(weatherCode)}");
        Console.WriteLine($"Rütubət         : {humidity} %");
        Console.WriteLine($"Küləyin sürəti  : {windSpeed.ToString("0.0", CultureInfo.InvariantCulture)} km/h");
        Console.WriteLine("----------------------------------");
    }

    private static async Task<string?> GetJsonAsync(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return await response.Content.ReadAsStringAsync();
        }

        string message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Yanlış sorğu göndərildi (400).",
            HttpStatusCode.Unauthorized => "İcazə yoxdur (401).",
            HttpStatusCode.Forbidden => "Giriş qadağandır (403).",
            HttpStatusCode.NotFound => "Məlumat tapılmadı (404).",
            HttpStatusCode.TooManyRequests => "Çox sayda sorğu göndərildi (429). Bir az gözləyin.",
            HttpStatusCode.InternalServerError => "Server xətası (500).",
            HttpStatusCode.ServiceUnavailable => "Xidmət müvəqqəti əlçatmazdır (503).",
            _ => $"Xəta baş verdi: {(int)response.StatusCode} {response.StatusCode}"
        };

        Console.WriteLine(message);
        return null;
    }

    private static string DescribeWeather(int code)
    {
        return code switch
        {
            0 => "Açıq səma",
            1 => "Əsasən açıq",
            2 => "Qismən buludlu",
            3 => "Buludlu",
            45 or 48 => "Dumanlı",
            51 or 53 or 55 => "Çiskin",
            56 or 57 => "Dondurucu çiskin",
            61 => "Yüngül yağış",
            63 => "Orta yağış",
            65 => "Güclü yağış",
            66 or 67 => "Dondurucu yağış",
            71 => "Yüngül qar",
            73 => "Orta qar",
            75 => "Güclü qar",
            77 => "Qar dənələri",
            80 or 81 or 82 => "Leysan yağışı",
            85 or 86 => "Qar leysanı",
            95 => "Tufan",
            96 or 99 => "Dolu ilə tufan",
            _ => "Naməlum"
        };
    }
}