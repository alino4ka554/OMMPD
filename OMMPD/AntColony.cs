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

        private bool flag = false;
        private Dictionary<int, Operation> _operations = new Dictionary<int, Operation>();
        private List<Operation> _listOfOperations = new List<Operation>();
        public Dictionary<(int, int), double> _pheromones = new Dictionary<(int, int), double>();
        public Dictionary<(int, int), double> _probabilities = new Dictionary<(int, int), double>();
        public ScheduleSolution BestSolution;
        private Random _rnd = new Random();
        private Dictionary<int, List<Operation>> _resourcesByOperations;
        private Dictionary<int, List<int>> _resourcesOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, List<int>> _projectsOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, Dictionary<int, List<int>>> _opsWithOneResInOneProj = new Dictionary<int, Dictionary<int, List<int>>>();
        private ScheduleSolution oldBest;
        public AntColony(List <Operation> operations, int iterations, int ants,
                         double beta, double alpha, double rho,
                         double tauMin, double tauMax)
        {
            _operations = operations.ToDictionary(op => op.Id);
            _listOfOperations = operations;
            _iterations = iterations;
            _ants = ants;
            _beta = beta;
            _alpha = alpha;
            _rho = rho;
            _tauMin = tauMin;
            _tauMax = tauMax;
            //_operations = _operations.ToDictionary(op => op.Id);
            OpsToProjects();
            OpsToResources();
            InitPheromones();
            //CalculateBeginTime(operations);
        }

        public void InitPheromones()
        {
            int b;
            foreach(var i in _operations.Values)
            {
                foreach(var j in _operations.Values)
                {
                    if (i.Id == 2 && j.Id == 6)
                        b = 0;
                    if((i != j) && i.Resource == j.Resource)
                    {
                        if(!i.DependsOn.Contains(j.Id) && !j.DependsOn.Contains(i.Id) && (i.Project != j.Project))
                            _pheromones.Add((i.Id, j.Id), _tauMax);
                    }
                }
            }
        }

        public void OrderingOneResInOneProj()
        {
            foreach(var res in _resourcesOperations)
            {
                if (!_opsWithOneResInOneProj.ContainsKey(res.Key))
                    _opsWithOneResInOneProj.Add(res.Key, new Dictionary<int, List<int>>());
                foreach (var project in _projectsOperations)
                {
                    if (!_opsWithOneResInOneProj[res.Key].ContainsKey(project.Key))
                       _opsWithOneResInOneProj[res.Key].Add(project.Key, new List<int>(res.Value.Where(op => _operations[op].Project == project.Key)).OrderBy(op => _operations[op].StartTime).ToList());
                }
            }
        }

        public void OpsToProjects()
        {
            Operation pred = null;
            foreach(var op in _operations.Values)
            {
                if(!_projectsOperations.ContainsKey(op.Project))
                    _projectsOperations.Add(op.Project, new List<int>());
                if(pred != null && pred.DependsOn.Contains(op.Id))
                    _projectsOperations[op.Project].Insert(_projectsOperations[op.Project].IndexOf(pred.Id), op.Id);
                else 
                    _projectsOperations[op.Project].Add(op.Id);
                pred = op;
            }
        }
        public void OpsToResources()
        {
            foreach(var op in _operations.Values)
            {
                if (!_resourcesOperations.ContainsKey(op.Resource))
                    _resourcesOperations.Add(op.Resource, new List<int>());
                _resourcesOperations[op.Resource].Add(op.Id);
            }
        }
        public ScheduleSolution BuildScheduleOfResources()
        {
            var operationsCopy = _operations.ToDictionary(
                kvp => kvp.Key,
                kvp => (Operation)kvp.Value.CloneOriginal());
            ScheduleSolution solution = new ScheduleSolution(operationsCopy, _pheromones);
            foreach (var res in _resourcesOperations)
            {
                var operations = new List<int>(res.Value);
                int pred = -1;
                if(res.Key == 0)
                    Console.WriteLine(res.Key.ToString());
                while(operations.Any())
                {
                    if (pred == -1)
                        pred = operations[_rnd.Next(operations.Count)];
                    if (operations.Count() == 1)
                    {
                        operations.Remove(pred);
                        break;
                    }
                    operations.Remove(pred);
                    var opsAfter = new List<int>(operations);
                    while(opsAfter.Any())
                    {
                        int after = operations[_rnd.Next(opsAfter.Count)];
                        if(opsAfter.Count() == 1)
                        {
                            operations.Remove(after);
                            var beginTime = operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime;
                            if (operationsCopy[after].StartTime < beginTime)
                            {
                                operationsCopy[after].StartTime = beginTime;
                                CalculateProjectsTimes(operationsCopy[after].Project, operationsCopy);
                            }
                            pred = after;
                            break;
                        }
                        if (_operations[pred].Project != _operations[after].Project)
                        {
                            double F = (operationsCopy[after].StartTime > operationsCopy[pred].StartTime) ? 2 : operationsCopy[after].StartTime == operationsCopy[pred].StartTime ? 1 : 0.5;
                            double pheij = _pheromones[(pred, after)];
                            double pheji = _pheromones[(after, pred)];
                            _probabilities[(pred, after)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                                / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                                + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
                            if (_rnd.NextDouble() <= _probabilities[(pred, after)])
                            {
                                var beginTime = operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime;
                                if (operationsCopy[after].StartTime < beginTime)
                                {
                                    operationsCopy[after].StartTime = beginTime;
                                    CalculateProjectsTimes(operationsCopy[after].Project, operationsCopy);
                                }
                                solution.W[(pred, after)] = 1;
                                operations.Remove(pred);
                                pred = after;
                                break;
                            }
                            else
                                opsAfter.Remove(after);
                        }
                        else
                        {
                            if(operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime > operationsCopy[after].StartTime + operationsCopy[after].ActualTime)
                                operations.Remove(after);
                            else
                            {
                                operations.Remove(pred);
                                pred = after;
                            }
                            break;
                        } 
                    }
                //    int randomId = operations[_rnd.Next(operations.Count)];
                //    if (operations.Count() == 1)
                //    {
                //        operations.Remove(randomId);
                //        break;
                //    }
                //    while (pred == randomId)
                //        randomId = operations[_rnd.Next(operations.Count)];
                //    if(operations.Count() == 2)
                //    {
                //        var beginTime = operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime;
                //        if (operationsCopy[randomId].StartTime < beginTime)
                //        {
                //            operationsCopy[randomId].StartTime = beginTime;
                //            CalculateProjectsTimes(operationsCopy[randomId].Project, operationsCopy);
                //        }
                //        solution.W[(pred, randomId)] = 1;
                //        operations.Remove(pred);
                //        pred = randomId;
                //    }
                //    else if (operationsCopy[pred].Project != operationsCopy[randomId].Project)
                //    {
                //        double F = (operationsCopy[randomId].StartTime > operationsCopy[pred].StartTime) ? 2 : operationsCopy[randomId].StartTime == operationsCopy[pred].StartTime ? 1 : 0.5;
                //        double pheij = _pheromones[(pred, randomId)];
                //        double pheji = _pheromones[(randomId, pred)];
                //        _probabilities[(pred, randomId)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                //            / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                //            + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
                //        if (_rnd.NextDouble() <= _probabilities[(pred, randomId)])
                //        {
                //            var beginTime = operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime;
                //            if (operationsCopy[randomId].StartTime < beginTime)
                //            {
                //                operationsCopy[randomId].StartTime = beginTime;
                //                CalculateProjectsTimes(operationsCopy[randomId].Project, operationsCopy);
                //            }
                //            solution.W[(pred, randomId)] = 1;
                //            operations.Remove(pred);
                //            pred = randomId;
                //        }
                //        /*else
                //        {
                //            var beginTime = operationsCopy[randomId].StartTime + operationsCopy[randomId].ActualTime;
                //            if (operationsCopy[pred].StartTime < beginTime)
                //                operationsCopy[pred].StartTime = beginTime;
                //            solution.W[(randomId, pred)] = 1;
                //            operations.Remove(randomId);
                //        }*/
                //    }
                //    else
                //    {
                //        if (operationsCopy[pred].StartTime + operationsCopy[pred].ActualTime > operationsCopy[randomId].StartTime + operationsCopy[randomId].ActualTime)
                //        {
                //            operations.Remove(randomId);
                           
                //        }
                //        else
                //        {
                //            operations.Remove(pred);
                //            pred = randomId;
                //        }
                //    }
                    
                }
            }
            
            return solution;
        }
        public void CalculateProjectsTimes(int projectId, Dictionary<int, Operation> operations)
        {
            var pred = _projectsOperations[projectId][0];
            foreach(var op in _projectsOperations[projectId])
            {
                if(op != pred)
                {
                    var beginTime = operations[pred].StartTime + operations[pred].ActualTime;
                    if (operations[op].StartTime < beginTime)
                        operations[pred].StartTime = beginTime;
                    pred = op;
                }
            }
        }
        public void CalculateFirstStartTimes()
        {
            foreach(var project in _projectsOperations)
            {
                foreach(var op in project.Value)
                {
                    if (_operations[op].DependsOn.Count == 0)
                        _operations[op].StartTime = 0;
                    else
                    {
                        var flag = 0.0;
                        foreach(var pred in _operations[op].DependsOn)
                        {
                            if (_operations[pred].StartTime + _operations[pred].ActualTime > flag)
                                flag = _operations[pred].StartTime + _operations[pred].ActualTime;
                        }
                        _operations[op].StartTime = flag;
                    }
                }
            }
        }
        public void CalculateEndTime(ScheduleSolution scheduleSolution)
        {
            var operations = scheduleSolution.Operations;
            var projectMaxEndTime = 0.0;
            foreach(var project in _projectsOperations)
            {
                
                foreach(var op in project.Value)
                {
                    var end = operations[op].StartTime + operations[op].ActualTime;
                    if(projectMaxEndTime < end)
                        projectMaxEndTime = end;
                }
            }
            scheduleSolution.TotalTime = projectMaxEndTime;
        }
        public Graph ConvertToGraph(ScheduleSolution solution)
        {
            var operations = solution.Operations;
            Graph graph = new Graph();
            foreach(var op in operations.Values)
            {
                if(op.DependsOn.Count != 0)
                {
                    foreach(var dep in op.DependsOn)
                    {
                        graph.AddEdge(dep, op.Id);
                    }
                }
            }
            //graph.PrintGraph();
            return graph;
        }

        public void Run()
        {
            //CalculateFirstBeginTime();
            CalculateFirstStartTimes();
            OrderingOneResInOneProj();
            ScheduleSolution oldSolution = null;
            for (int i = 0; i < _iterations; i++)
            {
                oldBest = null;
                for (int j = 0; j < _ants; j++)
                {
                    var solution = BuildScheduleOfResources();
                    CalculateEndTime(solution);
                    UpdateBest(solution);
                    Console.WriteLine($"Решение {j} муравья: лучшее время = {solution.TotalTime}");
                    /*var solution = BuildSolution();
                    CalculateBegin(solution);
                    CalculateTotalTime(solution);
                    UpdateBest(solution);
                    if(oldBest == null || oldBest.TotalTime > solution.TotalTime) 
                        oldBest = solution;*/
                    /*var solution = SetPriority();
                    CalculateStartTime(solution.Operations);
                    if (solution.Operations.Values == null)
                        break;
                    CalculateTotalTime(solution);
                    UpdateBest(solution);*/

                    //Console.WriteLine($"Решение {j} муравья: лучшее время = {solution.TotalTime}");
                }
                if (oldSolution == null || oldSolution.TotalTime != BestSolution.TotalTime)
                    flag = true;
                else 
                    flag = false;
                oldSolution = BestSolution;
                UpdatePheromones();

                Console.WriteLine($"Итерация {i}: лучшее время = {BestSolution.TotalTime}");
            }
        }
        public void CalculateFirstBeginTime()
        {
            var operationsCopy = _listOfOperations
                .Select(op => (Operation)op.Clone())
                .ToList();
            var operations = operationsCopy.ToDictionary(op => op.Id);
            ScheduleSolution solution = new ScheduleSolution(operations, _pheromones);
            CalculateBegin(solution);
        }

        public void CalculateBegin(ScheduleSolution solution)
        {
            Graph graph = ConvertToGraph(solution);
            var topologicalOperations = graph.TopologicalSort();
            var operations = solution.Operations;
            foreach(var op in topologicalOperations)
            {
                var operation = operations[op];
                if (operation.DependsOn.Count == 0)
                {
                    operation.StartTime = 0;
                    _operations[op].StartTime = operation.StartTime;
                }
                else
                {
                    double beginTime = 0;
                    foreach(var dep in  operation.DependsOn)
                    {
                        var timePred = operations[dep].StartTime + operations[dep].NormalTime;
                        if(timePred > beginTime)
                            beginTime = timePred;
                    }
                    operation.StartTime = beginTime;
                    _operations[op].StartTime = operation.StartTime;
                }
                //Console.WriteLine($"Operation #{op}, begintime = {operations[op].StartTime}");
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
            var operationsCopy = _listOfOperations
                .Select(op => (Operation)op.Clone())
                .ToList();
            var bestOperations = _operations;
            var operations = operationsCopy.ToDictionary(op => op.Id);
            ScheduleSolution solution = new ScheduleSolution(operations, _pheromones);
            foreach(var phe in _pheromones.Keys)
            {
                if (!operations[phe.Item1].DependsOn.Contains(phe.Item2) && !operations[(phe.Item2)].DependsOn.Contains(phe.Item1))
                {
                    double F = (_operations[phe.Item2].StartTime > _operations[phe.Item1].StartTime) ? 2 : _operations[phe.Item2].StartTime == _operations[phe.Item1].StartTime ? 1 : 0.5;
                    double pheij = _pheromones[(_operations[phe.Item1].Id, _operations[phe.Item2].Id)];
                    double pheji = _pheromones[(_operations[phe.Item2].Id, _operations[phe.Item1].Id)];
                    _probabilities[phe] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                        / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                        + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
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
                    CalculateBegin(solution);
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
            foreach (var phe in _pheromones.Keys)
            {
                double F = (operations[phe.Item2].StartTime > operations[phe.Item1].StartTime) ? 2 : operations[phe.Item2].StartTime == operations[phe.Item1].StartTime ? 1 : 0.5;
                double pheij = _pheromones[(operations[phe.Item1].Id, operations[phe.Item2].Id)];
                double pheji = _pheromones[(operations[phe.Item2].Id, operations[phe.Item1].Id)];
                _probabilities[phe] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                    / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                    + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
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
                kvp => (Operation)kvp.Value.Clone()
            );
            ScheduleSolution solution = new ScheduleSolution(operations, _pheromones);
            foreach(var op in operations)
            {
                if (op.Value.DependsOn.Count != 0)
                {
                    int flag = op.Value.Priority;
                    foreach(var j in op.Value.DependsOn)
                    {
                        if (operations[j].Priority >= flag)
                            flag += operations[j].Priority;
                    }
                    //op.Value.Priority = flag;
                }
                foreach (var phe in _pheromones)
                {
                    if (phe.Key.Item2 == op.Key)
                    {
                        var i = phe.Key.Item1;
                        if (!operations[i].DependsOn.Contains(op.Key) && !op.Value.DependsOn.Contains(i))
                        {
                            var Wij = DoesIDependsOnJ(i, op.Key, operations, solution);
                            if (!Wij)
                            {
                                if (op.Value.Priority >= operations[i].Priority)
                                    operations[i].Priority = Math.Max(operations[i].Priority, op.Value.Priority + 1);
                            }
                            else if (operations[i].Priority >= op.Value.Priority)
                                op.Value.Priority = Math.Max(op.Value.Priority, 1 + operations[i].Priority);
                        }
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
        public void Evaporate()
        {
            foreach(var ops in BestSolution.W.Keys)
            {
                _pheromones[ops] = Math.Max(_pheromones[ops] * (1 - _rho), _tauMin);
            }
        }
    }
}
