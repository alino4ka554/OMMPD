using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class CPM
    {
        public ScheduleSolution Solution;
        public double Td;
        public double E;

        public CPM(ScheduleSolution solution, double e)
        {
            Solution = solution;
            E = e;
            Td = Solution.TotalTime - E;
        }

        public void Run()
        {
            Solution.InitializeDependsForResource();
            Solution.FindCriticalWay();
            foreach (var criticalWay in Solution.CriticalWays)
            {
                Console.WriteLine($"Критический путь: {string.Join(", ", criticalWay.ToArray())}");
            }
            List<int> opsCannotBeAcceleratated = new List<int>();
            while(Solution.CriticalWays.Count != 0)
            {
                if (Solution.TotalTime <= Td)
                    Td = Solution.TotalTime - E;
                double min = double.MaxValue;
                int minOpId = -1;
                //if (Solution.CriticalWays.Count > 1)
                {
                    foreach (var op in Solution.Operations.Values)
                    {
                        int k = Solution.CriticalWays.Count(cw => cw.Contains(op.Id));
                        if (min > op.Delta / k && !opsCannotBeAcceleratated.Contains(op.Id))
                        {
                            min = op.Delta / k;
                            minOpId = op.Id;
                        }
                    }
                }
                //else
                  //  minOpId = Solution.CriticalWays[0].OrderBy(opId => Solution.Operations[opId].Delta).First();
                if(minOpId == -1)
                {
                    Solution.CriticalWays = new List<List<int>>();
                    break;
                }
                if (Solution.Operations[minOpId].Acceleration + (Solution.TotalTime - Td) <= Solution.Operations[minOpId].NormalTime - Solution.Operations[minOpId].CrashTime)
                {
                    Solution.Operations[minOpId].Acceleration += (Solution.TotalTime - Td);
                    Solution.RecalculateFrom(minOpId);
                }
                else
                    opsCannotBeAcceleratated.Add(minOpId);
                Solution.CriticalWays = new List<List<int>>();
                Solution.FindCriticalWay();
                foreach (var op in Solution.Operations)
                {
                    Console.WriteLine($"Операция № {op.Key}: время начала (Ti) = {op.Value.StartTime}");
                }
                foreach (var criticalWay in Solution.CriticalWays)
                {
                    Console.WriteLine($"Критический путь: {string.Join(", ", criticalWay.ToArray())}");
                }
            }
        }
    }
}
