using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class AntColony
    {
        private int _iterations;
        private int _ants;
        private double _beta;
        private double _alpha;
        private double _rho;
        private double _tauMin;
        private double _tauMax;


        private Dictionary<int, Operation> _operations = new Dictionary<int, Operation>();
        public Dictionary<(int, int), double> _pheromones = new Dictionary<(int, int), double>();
        public Dictionary<(int, int), double> _probabilities = new Dictionary<(int, int), double>();
        public ScheduleSolution BestSolution;
        private Random _rnd = new Random();
        private Dictionary<int, List<Operation>> _resourcesOperations;
        public AntColony(Dictionary<int, Operation> operations, int iterations, int ants,
                         double beta, double alpha, double rho,
                         double tauMin, double tauMax)
        {
            _operations = operations;
            _iterations = iterations;
            _ants = ants;
            _beta = beta;
            _alpha = alpha;
            _rho = rho;
            _tauMin = tauMin;
            _tauMax = tauMax;
            //_operations = _operations.ToDictionary(op => op.Id);

            InitPheromones();
            //CalculateBeginTime(operations);
        }

        public void InitPheromones()
        {
            foreach(var i in _operations.Values)
            {
                foreach(var j in _operations.Values)
                {
                    if((i != j) && i.Resource == j.Resource)
                    {
                        _pheromones.Add((i.Id, j.Id), _tauMax);
                    }
                }
            }
        }

        public void Run()
        {
            for(int i = 0; i < _iterations; i++)
            {
                CalculateProbability();
                for(int j = 0; j < _ants; j++)
                {
                    var solution = BuildSolution();
                    CalculateStartTimes(solution.Operations.Values.ToList());
                    if (solution.Operations.Values == null)
                        break;
                    CalculateTotalTime(solution);
                    UpdateBest(solution);
                    //Console.WriteLine($"Решение {j} муравья: лучшее время = {solution.TotalTime}");
                }
                UpdatePheromones();

                Console.WriteLine($"Итерация {i}: лучшее время = {BestSolution.TotalTime}");
            }
        }
        private void UpdateBest(ScheduleSolution solution)
        {
            if (BestSolution == null || solution.TotalTime < BestSolution.TotalTime)
            {
                BestSolution = solution;
            }
        }
        public static List<Operation> TopologicalSort(List<Operation> operations)
        {
            try
            {
                var result = new List<Operation>();
                var visited = new HashSet<int>();

                void Visit(Operation op)
                {
                    if (visited.Contains(op.Id))
                        return;

                    foreach (var predId in op.DependsOn)
                    {
                        var pred = operations.First(o => o.Id == predId);
                        Visit(pred);
                    }

                    visited.Add(op.Id);
                    result.Add(op);
                }

                foreach (var op in operations)
                    Visit(op);

                return result;
            }
            catch(Exception ex)
            {
                return null;
            }
        }
        public ScheduleSolution BuildSolution()
        {
            ScheduleSolution solution = new ScheduleSolution(_operations, _pheromones);
            Dictionary<int, Operation> operations = new Dictionary<int, Operation>(_operations);
            foreach(var phe in _pheromones.Keys)
            {
                if (!operations[phe.Item1].DependsOn.Contains(phe.Item2) && !operations[(phe.Item2)].DependsOn.Contains(phe.Item1))
                {
                    if (_rnd.NextDouble() <= _probabilities[(phe)])
                    {
                        solution.W[(phe)] = 1;
                        operations[phe.Item2].DependsOn.Add(phe.Item1);
                    }
                    else
                    {
                        solution.W[(phe)] = 0;
                        solution.W[(phe.Item2, phe.Item1)] = 1;
                        operations[phe.Item1].DependsOn.Add(phe.Item2);
                    }
                }
            }
            solution.Operations = operations;
            return solution;
        }
        public static void CalculateStartTimes(List<Operation> operations)
        {
            // Сортируем операции по топологическому порядку (чтобы предшественники шли раньше)
            var sorted = TopologicalSort(operations);
            if (sorted == null)
            {
                operations = null;
                return;
            }
            foreach (var op in sorted)
            {
                if (op.DependsOn.Count == 0)
                {
                    op.StartTime = 0; // если нет предшественников
                }
                else
                {
                    double maxEnd = 0;
                    foreach (var predId in op.DependsOn)
                    {
                        var pred = operations.First(o => o.Id == predId);
                        maxEnd = Math.Max(maxEnd, pred.EndTime);
                    }
                    op.StartTime = maxEnd;
                }
            }
        }

        public void ConstraintForBeginTime(int OpId)
        {
            foreach (var kvp in _operations)
            {
                if (kvp.Key != OpId)
                {
                    if (_operations[OpId].DependsOn.Contains(kvp.Key))
                    {
                        Operation op = _operations[kvp.Key];
                        var time = op.StartTime + op.NormalTime;
                        if (time > _operations[OpId].StartTime)
                            _operations[OpId].StartTime = time;
                    }
                }
            }
        }

        public void CalculateProbability()
        {
            var operations = (BestSolution == null) ? _operations : BestSolution.Operations;
            foreach (var i in operations.Values)
            {
                foreach (var j in operations.Values)
                {
                    if (i.Resource == j.Resource && i != j)
                    {
                        double F = (j.StartTime > i.StartTime) ? 2 : j.StartTime == i.StartTime ? 1 : 0.5;
                        double pheij = _pheromones[(i.Id, j.Id)];
                        double pheji = _pheromones[(j.Id, i.Id)];
                        _probabilities[(i.Id, j.Id)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)) 
                            / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha) 
                            + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
                    }
                }
            }
        }
        
        public void CalculateTotalTime(ScheduleSolution solution)
        {
            double totalTime = 0;
            foreach (var i in solution.Operations.Values)
            {
                var time = (i.StartTime + i.NormalTime);
                if(time > totalTime)
                    totalTime = time;
            }
            solution.TotalTime = totalTime;
        }

        public void UpdatePheromones()
        {
            foreach(var ops in BestSolution.W.Keys)
            {
                _pheromones[ops] = Math.Max(_pheromones[ops] * (1 - _rho), _tauMin);
                if (BestSolution.W[(ops)] == 1)
                {
                    _pheromones[ops] = Math.Min((_tauMax - _tauMin) * _rho + _pheromones[ops], _tauMax);
                }
            }
        }
    }
}
