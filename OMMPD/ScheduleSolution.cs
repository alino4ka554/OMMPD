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
            InitializeResource();
            //InitializeW();
        }
        public void InitializeW()
        {
            foreach(var op1 in Operations)
            {
                foreach(var op2 in Operations)
                {
                    if(op1.Key != op2.Key)
                    {
                        if (op1.Value.DependsOn.Contains(op2.Key))
                            W[(op2.Key, op1.Key)] = 1;
                    }
                }
            }
        }
        public void InitializeResource()
        {
            foreach (var op in Operations.Values)
            {
                if (!Resources.ContainsKey(op.Resource))
                {
                    Resources.Add(op.Resource, new Resource(op.Resource));
                    ResourceSequences.Add(op.Resource, new List<int>());
                }
            }
        }

        public void AddToResource(int op, int res)
        {
            if (Resources[res].Operations.Count != 0)
            {
                if (Operations[op].StartTime < Resources[res].ReleaseTime)
                {
                    Operations[op].StartTime = Resources[res].ReleaseTime;
                    
                    ConstraintForBeginTime(op);
                }
                W[(ResourceSequences[res].Last(), op)] = 1;
            }
            Resources[res].Operations.Add(Operations[op]);
            ResourceSequences[res].Add(op);
        }

        public void ConstraintForBeginTime(int OpId)
        {
            Operation op = Operations[OpId];
            var time = op.StartTime + op.NormalTime;
            foreach (var kvp in Operations)
            {
                if(kvp.Key != OpId)
                {
                    if (Operations[kvp.Key].DependsOn.Contains(OpId))
                    {

                        if (time > Operations[kvp.Key].StartTime)
                        {
                            Operations[kvp.Key].StartTime = time;
                            ConstraintForBeginTime(kvp.Key);
                        }
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
