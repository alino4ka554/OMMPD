using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class ScheduleSolution
    {
        public Dictionary<int, Operation> Operations { get; set; } = new Dictionary<int, Operation>();
        public Dictionary<int, int> Projiects { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, Resource> Resources { get; set; } = new Dictionary<int, Resource>();
        public double TotalTime { get; set; }
        public double TotalCost { get; set; }

        public Dictionary<int, int> CounterOfOperations { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, List<int>> ResourceSequences { get; set; } = new Dictionary<int, List<int>>();
        public Dictionary<(int, int), int> W { get; set; } = new Dictionary<(int, int), int>();

        public ScheduleSolution(Dictionary<int, Operation> _operations, Dictionary<(int, int), double> pheromones)
        {
            Operations = _operations;
            foreach(var phe in pheromones.Keys)
            {
                W.Add((phe), 0);
            }
            foreach (var op in Operations)
                CounterOfOperations.Add(op.Key, 0);
            //InitializeResource();
        }
        public void InitializeResource()
        {
            foreach (var op in Operations.Values)
            {
                if (!Resources.ContainsKey(op.Resource))
                    Resources.Add(op.Resource, new Resource(op.Resource));
                Resources[op.Resource].Operations.Add(op);
            }
        }
        public void ConstraintForBeginTime(int OpId)
        {
            foreach(var kvp in Operations)
            {
                if(kvp.Key != OpId)
                {
                    if (Operations[OpId].DependsOn.Contains(kvp.Key))
                    {
                        Operation op = Operations[kvp.Key];
                        var time = op.StartTime + op.NormalTime;
                        if (time > Operations[OpId].StartTime)
                            Operations[OpId].StartTime = time;
                    }
                }
            }
        }

        public void ConstraintForOneResource(int i, int j)
        {
            int w = W[(i, j)];
            double T_i = Operations[i].StartTime;
            double T_j = Operations[j].StartTime;
            double t_i = Operations[i].NormalTime;
            double t_j = Operations[j].NormalTime;
            if (T_j - T_i - 999 * w <= -t_i && T_i - T_j + 999 * w <= 999 - t_j)
                return;
            else 
                W[(i, j)] = (w == 0) ? 1 : 0;
        }

    }
}
