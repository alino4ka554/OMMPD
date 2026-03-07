using System;
using Aspose.Cells;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace OMMPD
{
   
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Тестирование гибридного алгоритма муравьиной колонии");

            string filePath = "operations.xlsx";

            var ops = LoadOperationsFromExcel(filePath);

            Stopwatch sw = new Stopwatch();
            var operations = ops.ToDictionary(op => op.Id);

            /*var parameters = new AntColonyParameters
            {
                Iterations = 100,  // Уменьшено для тестирования
                AntsCount = 50,    // Уменьшено для тестирования
                Alpha = 1.2,
                Beta = 3.0,
                MaxPheromone = 1.0,
                MinPheromone = 0.01,
                EvaporationRate = 0.1
            };*/
            var colony = new ACO(ops, iterations: 50, ants: 10,
                                       beta: 20, alpha: 4, rho: 0.01,
                                       tauMin: 0.01, tauMax: 1.0);
            sw.Start();
            colony.Run();
            sw.Stop();
            double elapsedSeconds = sw.Elapsed.TotalSeconds;


            Console.WriteLine("\n=== РЕЗУЛЬТАТЫ ===");
            Console.WriteLine($"Total time = {colony.BestSolution.TotalTime}");
            Console.WriteLine($"Время выполнения программы: {elapsedSeconds:F3} сек.");
            /*try
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }*/

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
        //static void Main(string[] args)
        //{
        //    SalesmanProblem problem = new SalesmanProblem();
        //    problem.Run();
        //}
        public static List<Operation> LoadOperationsFromExcel(string path)
        {
            var operations = new List<Operation>();
            Workbook wb = new Workbook(path);
            WorksheetCollection collection = wb.Worksheets;
            for (int worksheetIndex = 5; worksheetIndex < 6; worksheetIndex++)
            {
                Worksheet worksheet = collection[worksheetIndex];
                int rows = worksheet.Cells.MaxDataRow;
                int cols = worksheet.Cells.MaxDataColumn;
                for (int i = 1; i <= rows; i++)
                {
                    var preds = new List<int>();
                    var cellValue = worksheet.Cells[i, 1].Value?.ToString().Trim();
                    if (!string.IsNullOrEmpty(cellValue) && cellValue != "-")
                    {
                        preds = cellValue
                            .Split(',')                        // разделяем по запятой
                            .Select(x => x.Trim())             // убираем пробелы
                            .Where(x => int.TryParse(x, out _))// оставляем только корректные числа
                            .Select(int.Parse)                 // переводим в int
                            .ToList();
                    }

                    var op = new Operation
                    {
                        Id = int.TryParse(worksheet.Cells[i, 0].Value?.ToString(), out var idVal) ? idVal : 0,
                        DependsOn = preds,
                        Resource = int.TryParse(worksheet.Cells[i, 3].Value?.ToString(), out var resVal) ? resVal : 0,
                        Project = int.TryParse(worksheet.Cells[i, 2].Value?.ToString(), out var prVal) ? prVal : 0,
                        NormalTime = double.TryParse(worksheet.Cells[i, 4].Value?.ToString(), out var ntVal) ? ntVal : 0,

                    };
                    operations.Add(op);
                }
            }
            return operations;
        }
    }
}