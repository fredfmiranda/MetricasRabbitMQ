using DatadogStatsD;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StatsdClient;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MetricasRabbitMQ
{
    class Program
    {
        static IConnection connection;
        static IModel channel;

        static Dictionary<string, int> fileCounter = new Dictionary<string, int>();
        static Dictionary<string, string> appGroups = new Dictionary<string, string>
        {
            { "RecargaIVR", "RecargaGroup" },
            { "RecargaSMS", "RecargaGroup" }
        };
        static int minFilesExpectedPerHour = 5;
        static int maxFilesReceivedPerHour = 20;

        static void Main(string[] args)
        {
            // Configuração do StatsD
            DogStatsd.Configure(new StatsdConfig { StatsdServerName = "127.0.0.1", StatsdPort = 8125 });

            var factory = new ConnectionFactory() { HostName = "localhost", Port = 5672 };
            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            channel.QueueDeclare(queue: "fileQueue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                HandleMessage(message);
            };

            // Subscrever para eventos de desligamento e exceções
            connection.ConnectionShutdown += OnConnectionShutdown;
            channel.CallbackException += OnCallbackException;

            channel.BasicConsume(queue: "fileQueue",
                                 autoAck: true,
                                 consumer: consumer);

            Console.WriteLine("Pressione [enter] para encerrar.");
            Console.ReadLine();
        }

        static void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            Console.WriteLine("Conexão derrubada. Tentativa de reconexão...");
            Reconnect();
        }

        static void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            Console.WriteLine($"Callback exceção: {e.Exception}. Tentativa de reconexão...");
            Reconnect();
        }

        static void Reconnect()
        {
            var factory = new ConnectionFactory() { HostName = "localhost", Port = 5672 };
            bool connected = false;

            while (!connected)
            {
                try
                {
                    connection = factory.CreateConnection();
                    channel = connection.CreateModel();
                    connected = true;
                    Console.WriteLine("Reconexão bem sucedida.");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Reconexão falhou: {e.Message}. Tentando novamente em 5 segundos...");
                    Thread.Sleep(5000);
                }
            }
        }

        static void SendEmailAlert(string subject, string body)
        {
            MailMessage mail = new MailMessage("fredfmiranda@gmail.com", "emaildestinatario@gmail.com");
            SmtpClient client = new SmtpClient();
            client.Port = 25;
            client.Host = "smtp.example.com";
            mail.Subject = subject;
            mail.Body = body;
            client.Send(mail);
        }

        static void HandleMessage(string message)
        {
            try
            {
                var payload = JObject.Parse(message);
                var filename = payload["filename"].ToString();
                var regex = new Regex(@"^(.+?)_(.+?)_\d{14}.txt$");
                var match = regex.Match(filename);

                if (match.Success)
                {
                    var machineName = match.Groups[1].Value;
                    var appName = match.Groups[2].Value;
                    var currentHour = DateTime.Now.Hour;

                    if (appGroups.ContainsKey(appName))
                    {
                        appName = appGroups[appName];
                    }

                    string appHourKey = $"{appName}_{currentHour}";

                    if (!fileCounter.ContainsKey(appHourKey))
                    {
                        fileCounter[appHourKey] = 0;
                    }

                    fileCounter[appHourKey] += 1;
                    int currentCount = fileCounter[appHourKey];

                    if (currentCount < minFilesExpectedPerHour)
                    {
                        SendEmailAlert("Alerta: Gap detectado", $"Quantidade baixa de arquivo detectada para {appName} no horário {currentHour}");
                    }
                    else if (currentCount > maxFilesReceivedPerHour)
                    {
                        SendEmailAlert("Alerta: Burst detectado", $"Quantidade alta de arquivo detectada para {appName} no horário {currentHour}");
                    }

                    Console.WriteLine($"count = {currentCount} for file {appName} at hour {currentHour} on {DateTime.Now}");

                    DogStatsd.Increment($"file_count", 1, tags: new[] { $"appName:{appName}", $"hour:{currentHour}" });
                }
                else
                {
                    Console.WriteLine($"Nome de arquivo inválido: {filename}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Falha ao manipular mensagem: {e.Message}");
            }
        }
    }
}
