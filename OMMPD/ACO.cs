using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class ACO
    {
        private int _iterations;
        private int _ants;
        private double _beta;
        private double _alpha;
        private double _rho;
        private double _tauMin;
        private double _tauMax;
        private double _Q;
        private Dictionary<int, List<int>> _probableWay = new Dictionary<int, List<int>>();
        private Dictionary<int, double> _resourceFree = new Dictionary<int, double>();
        private Dictionary<int, Operation> _operations = new Dictionary<int, Operation>();
        private Dictionary<int, Operation> ops = new Dictionary<int, Operation>();
        public Dictionary<(int, int), double> _pheromones = new Dictionary<(int, int), double>();
        public Dictionary<(int, int), double> _localPheromones = new Dictionary<(int, int), double>();
        public Dictionary<(int, int), double> _probabilities = new Dictionary<(int, int), double>();
        public ScheduleSolution BestSolution;
        private Random _rnd = new Random();
        private Dictionary<int, List<int>> _resourcesOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, List<int>> _projectsOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, Dictionary<int, List<int>>> _opsWithOneResInOneProj = new Dictionary<int, Dictionary<int, List<int>>>();
        private ScheduleSolution oldBest;
        public double GetRandomChoice() => _rnd.NextDouble();

        public ACO(List<Operation> operations, int iterations, int ants,
                         double beta, double alpha, double rho,
                         double tauMin, double tauMax)
        {
            _operations = operations.ToDictionary(op => op.Id);
            ops = operations.ToDictionary(op => op.Id);
            _iterations = iterations;
            _ants = ants;
            _beta = beta;
            _alpha = alpha;
            _rho = rho;
            _tauMin = tauMin;
            _tauMax = tauMax;
            _Q = CalculateQ(operations);
            OpsToProjects();
            OpsToResources();
            InitPheromones();
            OrderingOneResInOneProj();
            ConvertToGraph();
            //CalculateBeginTime(operations);
        }
        private double CalculateQ(List<Operation> operations)
        {
            double totalWork = operations.Sum(op => op.ActualTime);

            double minPossibleTime = operations
                .GroupBy(op => op.Project)
                .Max(g => g.Sum(op => op.ActualTime)); 

            double resourceTime = operations
                .GroupBy(op => op.Resource)
                .Max(g => g.Sum(op => op.ActualTime)); 

            double estimatedMakespan = Math.Max(minPossibleTime, resourceTime);

            return 0.02 * estimatedMakespan; 
        }
        public void InitPheromones()
        {
            foreach (var i in _operations.Values)
            {
                foreach (var j in _operations.Values)
                {
                    
                    //if ((i != j) && i.Resource == j.Resource)
                    if(i != j)
                    {
                        _localPheromones.Add((i.Id, j.Id), 0);
                        _pheromones.Add((i.Id, j.Id), _tauMax);
                    }
                }
            }
        }
        //private void InitAnts(int ants)
        //{
        //    for(int i = 0; i <= _operations.Count; i++)
        //    {
        //        if (i <= ants)
        //            _ants.Add(i + 1, i);
        //    }
        //}
        public void OpsToProjects()
        {
            foreach(var op in _operations.Values)
            {
                if (!_projectsOperations.ContainsKey(op.Project))
                    _projectsOperations.Add(op.Project, new List<int>());
                _projectsOperations[op.Project].Add(op.Id);
            }
        }

        public void SortingForProjects()
        {
            List<int> depends = new List<int>();
            while (ops.Count > 0)
            {
                foreach (var op in ops.Values)
                {
                    if (op.DependsOn.Count == 0)
                    {
                        _operations.Add(op.Id, op);
                        depends.Add(op.Id);
                        ops.Remove(op.Id);
                    }
                    else if (depends.Count != 0 && op.DependsOn.All(dep => depends.Contains(dep)))
                    {
                        _operations.Add(op.Id, op);
                        depends.Add(op.Id);
                        ops.Remove(op.Id);
                    }
                }
            }
        }
        public void OpsToResources()
        {
            foreach(var op in _operations.Values)
            {
                if(!_resourcesOperations.ContainsKey(op.Resource))
                    _resourcesOperations.Add(op.Resource, new List<int>());
                _resourcesOperations[op.Resource].Add(op.Id);
                if (!_resourceFree.ContainsKey(op.Resource))
                    _resourceFree.Add(op.Resource, 0);
            }
        }
        public void OrderingOneResInOneProj()
        {
            foreach (var res in _resourcesOperations)
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

        public ScheduleSolution BuildSolution()
        {
            var operationsCopy = _operations.ToDictionary(
                kvp => kvp.Key,
                kvp => (Operation)kvp.Value.CloneOriginal());
            ScheduleSolution solution = new ScheduleSolution(operationsCopy, _pheromones);
            foreach(var res in  _resourcesOperations)
            {
                var operationsByResource = new List<int>(res.Value);
                var currentOp = operationsByResource[(int)(GetRandomChoice() * operationsByResource.Count)];
                var opsWithOneProj = new Dictionary<int, List<int>>();
                foreach (var kvp in _opsWithOneResInOneProj[res.Key])
                {
                    opsWithOneProj[kvp.Key] = new List<int>(kvp.Value);
                }
                operationsByResource.Remove(currentOp);
                List<int> visited = new List<int>();
                visited.Add(currentOp);
                var currentOpsInOneProject = new List<int>(opsWithOneProj[_operations[currentOp].Project]);
                if (currentOpsInOneProject.Count != 0 && currentOpsInOneProject.IndexOf(currentOp) != 0)
                {
                    visited.Remove(currentOp);
                    operationsByResource.Add(currentOp);
                    //operationsCopy[currentOp].StartTime = _operations[currentOp].StartTime;
                    currentOp = currentOpsInOneProject.First();
                    visited.Add(currentOp);
                    operationsByResource.Remove(currentOp);
                }
                opsWithOneProj[_operations[currentOp].Project].Remove(currentOp);
                while (operationsByResource.Any())
                {
                    var nextOp = CalculateNextOperation(currentOp, operationsByResource, operationsCopy);
                    var nextOpsInOneProject = opsWithOneProj[_operations[nextOp].Project];
                    if (nextOpsInOneProject.Count == 0 || nextOpsInOneProject.IndexOf(nextOp) == 0)
                        AddOperationToVisited(ref currentOp, ref nextOp, operationsCopy, visited, solution, operationsByResource);
              
                    else if (nextOpsInOneProject.Count != 0 && nextOpsInOneProject.IndexOf(nextOp) != 0)
                    {
                        nextOp = nextOpsInOneProject.First();
                        AddOperationToVisited(ref currentOp, ref nextOp, operationsCopy, visited, solution, operationsByResource);
                    }
                    opsWithOneProj[_operations[nextOp].Project].Remove(nextOp);
                }

                solution.ResourceSequences.Add(res.Key, new List<int>(visited));
            }
            return solution;
        }
        public void ConvertToGraph()
        {
            foreach(var op in _operations.Values)
            {
                if(op.DependsOn.Count != 0)
                {
                    foreach (var op2 in op.DependsOn)
                    {
                        if (!_probableWay.ContainsKey(op2))
                            _probableWay.Add(op2, new List<int> ());
                        _probableWay[op2].Add(op.Id);
                    }
                }
                if (!_probableWay.ContainsKey(op.Id))
                    _probableWay.Add(op.Id, new List<int>());
                _probableWay[op.Id].AddRange(_resourcesOperations[op.Resource].Where(ops => ops != op.Id && op.Project != _operations[ops].Project));
            }
        }
        public bool IsAOps(Operation first, int second)
        {
            var _ops = _opsWithOneResInOneProj[first.Resource][first.Project];
            if (!_ops.Contains(second))
                return true;
            else if (_ops.IndexOf(first.Id) < _ops.IndexOf(second))
                return true;
            return false;

        }
        public ScheduleSolution BuildSolution2()
        {
            var operationsCopy = _operations.ToDictionary(
                kvp => kvp.Key,
                kvp => (Operation)kvp.Value.CloneOriginal());
            ScheduleSolution solution = new ScheduleSolution(operationsCopy, _pheromones);
            
            var rnd = GetRandomChoice();
            var currentOp = operationsCopy[(int)(rnd * operationsCopy.Count) + 1].Id;
            var notVisitedOps = new List<int>(_operations.Keys);
            notVisitedOps.Remove(currentOp);
            while(notVisitedOps.Any())
            {
                var opsWithOneProj = new Dictionary<int, List<int>>();
                foreach (var kvp in _opsWithOneResInOneProj[operationsCopy[currentOp].Resource])
                {
                    opsWithOneProj[kvp.Key] = new List<int>(kvp.Value);
                }
                var currentOpsInOneProject = new List<int>(opsWithOneProj[_operations[currentOp].Project]);
                if (currentOpsInOneProject.Count != 0 && currentOpsInOneProject.IndexOf(currentOp) != 0 && notVisitedOps.Contains(currentOpsInOneProject[0]))
                {
                    notVisitedOps.Add(currentOp);
                    //operationsCopy[currentOp].StartTime = _operations[currentOp].StartTime;
                    currentOp = currentOpsInOneProject.First();
                    notVisitedOps.Remove(currentOp);
                }
                opsWithOneProj[_operations[currentOp].Project].Remove(currentOp);
                if (!solution.ResourceSequences.ContainsKey(_operations[currentOp].Resource))
                    solution.ResourceSequences.Add(_operations[currentOp].Resource, new List<int>());
                solution.ResourceSequences[_operations[currentOp].Resource].Add(currentOp);
                var probablyOps = IsContainsOps(notVisitedOps, _probableWay[currentOp]);
                if (probablyOps.Count > 0)
                {
                    var nextOp = CalculateNextOperation(currentOp, probablyOps, operationsCopy);
                    var nextOpsInOneProject = new List<int>(opsWithOneProj[_operations[nextOp].Project]);
                    if (nextOpsInOneProject.Count == 0 || nextOpsInOneProject.IndexOf(nextOp) == 0)
                    {
                        var beginTime = operationsCopy[currentOp].StartTime + operationsCopy[currentOp].ActualTime;
                        if (operationsCopy[nextOp].StartTime < beginTime)
                        {
                            operationsCopy[nextOp].StartTime = beginTime;
                            CalculateProjectsTimes(operationsCopy[nextOp].Project, operationsCopy, solution);
                        }
                        solution.W[(currentOp, nextOp)] = 1;
                        currentOp = nextOp;
                        notVisitedOps.Remove(nextOp);
                        currentOp = nextOp;
                    }

                    else if (nextOpsInOneProject.Count != 0 && nextOpsInOneProject.IndexOf(nextOp) != 0)
                    {
                        nextOp = nextOpsInOneProject.First();

                        var beginTime = operationsCopy[currentOp].StartTime + operationsCopy[currentOp].ActualTime;
                        if (operationsCopy[nextOp].StartTime < beginTime)
                        {
                            operationsCopy[nextOp].StartTime = beginTime;
                            CalculateProjectsTimes(operationsCopy[nextOp].Project, operationsCopy, solution);
                        }
                        solution.W[(currentOp, nextOp)] = 1;
                        currentOp = nextOp;
                        notVisitedOps.Remove(nextOp);
                        currentOp = nextOp;
                    }
                    opsWithOneProj[_operations[nextOp].Project].Remove(nextOp);
                    _resourceFree[_operations[currentOp].Resource] = operationsCopy[currentOp].EndTime;
                    _resourceFree[_operations[nextOp].Resource] = operationsCopy[nextOp].EndTime;

                }
                else
                {
                    currentOp = notVisitedOps[(int)(GetRandomChoice() * notVisitedOps.Count)];
                    notVisitedOps.Remove(currentOp);
                }
            }
            return solution;
        }
        public List<int> IsContainsOps(List<int> notVisited, List<int> operations)
        {
            List<int> probablyOps = new List<int>();
            foreach(var op in operations)
            {
                if (notVisited.Contains(op))
                    probablyOps.Add(op);
            }
            return probablyOps;
        }
        public void AddOperationToVisited(ref int currentOp, ref int nextOp, Dictionary<int, Operation> operationsCopy, List <int> visited, ScheduleSolution solution, List<int> operationsByResource)
        {
            var beginTime = operationsCopy[currentOp].StartTime + operationsCopy[currentOp].ActualTime;
            if (operationsCopy[nextOp].StartTime < beginTime)
            {
                operationsCopy[nextOp].StartTime = beginTime;
                solution.CounterOfOperations[nextOp] += 1;
                CalculateProjectsTimes(operationsCopy[nextOp].Project, operationsCopy, solution);
            }
            solution.W[(currentOp, nextOp)] = 1;
            currentOp = nextOp;
            operationsByResource.Remove(nextOp);
            visited.Add(nextOp);
        }
        public int CalculateNextOperation(int currentOp, List<int> operations, Dictionary<int, Operation> currentOperations)
        {
            Dictionary<int, double> probabilities = new Dictionary<int, double>();
           // double sum = operations.Sum(op => _operations[op].StartTime);
            double summary = 0;
            foreach(var operation in operations)
            {
                double F = 1 / currentOperations[operation].StartTime ;
                double pheij = _pheromones[(currentOp, operation)];
                _probabilities[(currentOp, operation)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha));
                probabilities[operation] = _probabilities[(currentOp, operation)];
                summary += _probabilities[(currentOp, operation)];
            }
            var randomValue = GetRandomChoice();
            double cumulative = 0;
            foreach (var probability in probabilities)
            {
                cumulative += probability.Value / summary;
                if (randomValue <= cumulative)
                    return probability.Key;
            }
            return operations.Last();
        }
        public void CalculateProjectsTimes(int projectId, Dictionary<int, Operation> operations, ScheduleSolution solution)
        {
            var pred = _projectsOperations[projectId][0];
            foreach (var op in _projectsOperations[projectId])
            {
                if (op != pred)
                {
                    var beginTime = operations[pred].StartTime + operations[pred].ActualTime;
                    if (operations[op].StartTime < beginTime)
                    {
                        operations[op].StartTime = beginTime;
                        solution.CounterOfOperations[op] += 1;
                    }
                    pred = op;
                }
            }
        }
        public void Run()
        {
            CalculateFirstStartTimes();
            
            for(int i = 0; i <= _iterations; i++)
            {
                for(int j = 0; j <= _ants; j++)
                {
                    var solution = BuildSolution2();
                    CalculateEndTime(solution);
                    UpdateBest(solution);
                    LocalUpdatePheromones(solution);
                    Console.WriteLine($"Решение {j} муравья: лучшее время = {solution.TotalTime}");
                }
                GlobalUpdatePheromones();

                Console.WriteLine($"Итерация {i}: лучшее время = {BestSolution.TotalTime}");
            }
        }
        public void CalculateEndTime(ScheduleSolution scheduleSolution)
        {
            var operations = scheduleSolution.Operations;
            var projectMaxEndTime = 0.0;
            foreach (var project in _projectsOperations)
            {

                foreach (var op in project.Value)
                {
                    var end = operations[op].StartTime + operations[op].ActualTime;
                    if (projectMaxEndTime < end)
                        projectMaxEndTime = end;
                }
            }
            scheduleSolution.TotalTime = projectMaxEndTime;
        }
        public void CalculateFirstStartTimes()
        {
            foreach (var project in _projectsOperations)
            {
                foreach (var op in project.Value)
                {
                    if (_operations[op].DependsOn.Count == 0)
                        _operations[op].StartTime = 0;
                    else
                    {
                        var flag = 0.0;
                        foreach (var pred in _operations[op].DependsOn)
                        {
                            if (_operations[pred].StartTime + _operations[pred].ActualTime > flag)
                                flag = _operations[pred].StartTime + _operations[pred].ActualTime;
                        }
                        _operations[op].StartTime = flag;
                    }
                }
            }
        }
        private void UpdateBest(ScheduleSolution solution)
        {
            if (BestSolution == null || solution.TotalTime < BestSolution.TotalTime)
            {
                BestSolution = solution;
            }
        }
        public void GlobalUpdatePheromones()
        {
            foreach (var ops in BestSolution.W.Keys)
            {
                _pheromones[ops] = Math.Max(_pheromones[ops] * (1 - _rho) + _localPheromones[ops], _tauMin);
                //if (BestSolution.W[(ops)] == 1)
                //{
                //    _pheromones[ops] = Math.Min((_tauMax - _tauMin) * _rho + _pheromones[ops], _tauMax);
                //}
            }
        }
        public void LocalUpdatePheromones(ScheduleSolution solution)
        {
            foreach (var ops in BestSolution.W.Keys)
            {
                if (solution.W[(ops)] == 1)
                    _localPheromones[ops] += _Q / solution.TotalTime;
            }
        }
    }
}
