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
                    if((i != j) && i.Resource == j.Resource && !i.DependsOn.Contains(j.Id) && !j.DependsOn.Contains(i.Id))
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
                for(int j = 0; j < _ants; j++)
                {
                    var solution = SetPriority();
                    CalculateStartTime(solution.Operations);
                    if (solution.Operations.Values == null)
                        break;
                    CalculateTotalTime(solution);
                    UpdateBest(solution);
                    Console.WriteLine($"Решение {j} муравья: лучшее время = {solution.TotalTime}");
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
        private void CalculateBeginTime(List<Operation> operations)
        {
            // Сбрасываем времена
            foreach (var op in operations)
            {
                op.StartTime = -1;
                op.IsRunning = false;
            }

            // Пока есть хотя бы одна операция без BeginTime
            bool updated;
            do
            {
                updated = false;

                foreach (var operation in operations)
                {
                    if (operation.StartTime >= 0) continue;

                    var preds = operations.Where(o => operation.DependsOn.Contains(o.Id)).ToList();

                    if (!preds.Any()) // без предков
                    {
                        operation.StartTime = 0;
                        updated = true;
                    }
                    else if (preds.All(p => p.StartTime >= 0))
                    {
                        operation.StartTime = preds.Max(p => p.EndTime);
                        updated = true;
                    }
                }
            } while (updated);
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
        public ScheduleSolution SetPriority()
        {
            var operations = _operations.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()
            );
            ScheduleSolution solution = new ScheduleSolution(operations, _pheromones);
            foreach(var op in operations)
            {
                if (op.Value.DependsOn.Count != 0)
                {
                    int flag = op.Value.Priority;
                    foreach(var j in op.Value.DependsOn)
                    {
                        if (operations[j].Priority > flag)
                            flag += operations[j].Priority;
                    }
                }
                foreach (var phe in _pheromones)
                {
                    if (phe.Key.Item2 == op.Key)
                    {
                        var i = phe.Key.Item1;
                        var Wij = DoesIDependsOnJ(i, op.Key, operations, solution);
                        if (!Wij)
                            operations[i].Priority += op.Value.Priority;
                        else if (operations[i].Priority > op.Value.Priority)
                                op.Value.Priority += operations[i].Priority;
                    }
                }
            }
            return solution;
        }
        public void CalculateStartTime(Dictionary<int, Operation> operations)
        {
            foreach (var op in operations.Values)
                op.StartTime = -1;

            // 2. Сортируем приоритеты (1, 2, 3, ...)
            var priorityLevels = operations.Values
                .Select(o => o.Priority)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // 3. Идём по уровням приоритета
            foreach (var priority in priorityLevels)
            {
                var currentOps = operations.Values
                    .Where(o => o.Priority == priority)
                    .ToList();

                foreach (var op in currentOps)
                {
                    // 3.1. Начальное время = конец всех предшественников
                    double start = 0;

                    if (op.DependsOn.Any())
                    {
                        var predEnd = op.DependsOn
                            .Select(id => operations[id].StartTime + operations[id].NormalTime)
                            .DefaultIfEmpty(0)
                            .Max();

                        start = Math.Max(start, predEnd);
                    }

                    // 3.2. Учитываем операции, использующие тот же ресурс
                    var sameResource = operations.Values
                        .Where(o => o.Resource == op.Resource &&
                                    o.Priority <= op.Priority &&
                                    o.Id != op.Id &&
                                    o.StartTime >= 0)
                        .Select(o => o.StartTime + o.NormalTime)
                        .DefaultIfEmpty(0)
                        .Max();

                    start = Math.Max(start, sameResource);

                    // 3.3. Устанавливаем время начала
                    op.StartTime = start;
                    _operations[op.Id].StartTime = start;
                }
            }
        }
        public void HaveTheCycle(Dictionary<int, Operation> operations)
        {
            foreach(var op in operations)
            {
                foreach(var j in op.Value.DependsOn)
                {
                    if (operations[j].Priority > op.Value.Priority)
                        op.Value.DependsOn.Remove(j);
                }
            }
        }
        public bool DoesIDependsOnJ(int i, int j, Dictionary<int, Operation> operations, ScheduleSolution solution)
        {
            double F = (_operations[j].StartTime > _operations[i].StartTime) ? 2 : _operations[j].StartTime == _operations[i].StartTime ? 1 : 0.5;
            double pheij = _pheromones[(i, j)];
            double pheji = _pheromones[(j, i)];
            _probabilities[(i, j)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
            if (_rnd.NextDouble() <= _probabilities[(i, j)])
            {
                solution.W[(i, j)] = 1;
                operations[j].DependsOn.Add(i);
                return true;
            }
            else
            {
                solution.W[(i, j)] = 0;
                solution.W[(j, i)] = 1;
                operations[i].DependsOn.Add(j);
                return false;
            }

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
