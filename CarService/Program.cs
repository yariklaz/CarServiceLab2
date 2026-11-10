using System;
using System.Diagnostics;
using Npgsql;
using MongoDB.Driver;
using MongoDB.Bson;
using StackExchange.Redis;

namespace AutoServiceApp
{
    class Program
    {
        static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            int iterations = 10000;
            int vehicleIdToFind = 2;

            Console.WriteLine($"Запуск тесту швидкодії (Читання історії ТО {iterations} разів)...\n");

            string pgConnectionString = "Host=localhost;Database=AutoService;Username=postgres;Password=+L4679013y";

            Stopwatch pgTimer = new Stopwatch();
            using (var pgConnection = new NpgsqlConnection(pgConnectionString))
            {
                pgConnection.Open();

                string sqlQuery = @"
                    SELECT mr.record_id, mr.description, mp.quantity, sp.part_name 
                    FROM maintenance_records mr
                    JOIN maintenance_parts mp ON mr.record_id = mp.record_id
                    JOIN spare_parts sp ON mp.part_id = sp.part_id
                    WHERE mr.vehicle_id = @vId";

                using (var cmd = new NpgsqlCommand(sqlQuery, pgConnection))
                {
                    cmd.Parameters.AddWithValue("vId", vehicleIdToFind);
                    cmd.Prepare();

                    pgTimer.Start();
                    for (int i = 0; i < iterations; i++)
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) { /* Імітуємо читання даних */ }
                        }
                    }
                    pgTimer.Stop();
                }
            }
            Console.WriteLine($"[SQL] PostgreSQL (з JOIN 3-х таблиць) виконав запити за: {pgTimer.ElapsedMilliseconds} мс");
            string mongoConnectionString = "mongodb://localhost:27017";
            var mongoClient = new MongoClient(mongoConnectionString);
            var database = mongoClient.GetDatabase("FleetManagement");
            var collection = database.GetCollection<BsonDocument>("maintenance_history");

            var filter = Builders<BsonDocument>.Filter.Eq("vehicle_id", vehicleIdToFind);

            Stopwatch mongoTimer = new Stopwatch();
            mongoTimer.Start();
            for (int i = 0; i < iterations; i++)
            {
                var result = collection.Find(filter).ToList();
            }
            mongoTimer.Stop();

            Console.WriteLine($"[NoSQL] MongoDB (читання готового документа) виконала запити за: {mongoTimer.ElapsedMilliseconds} мс");

            Console.WriteLine("\n[Redis] Підключення до бази Key-Value...");

            try
            {
                var redis = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost");
                var db = redis.GetDatabase();

                string redisKey = "employee_status:1"; 
                string redisValue = "На зміні";

                db.StringSet(redisKey, redisValue, TimeSpan.FromHours(12)); 
                Console.WriteLine($"[Redis] Збережено статус для водія (ID 1): '{redisValue}'");

                string cachedStatus = db.StringGet(redisKey);
                Console.WriteLine($"[Redis] Прочитано статус з кешу: '{cachedStatus}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Redis] Помилка підключення: {ex.Message}");
            }
            Console.WriteLine("\nТест завершено. Натисніть Enter...");
            Console.ReadLine();
        }
    }
}