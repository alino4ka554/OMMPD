using System;
using System.Collections.Generic;
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
        private Dictionary<int, Operation> _operations = new Dictionary<int, Operation>();
        private Dictionary<int, Operation> ops = new Dictionary<int, Operation>();
        public Dictionary<(int, int), double> _pheromones = new Dictionary<(int, int), double>();
        public ScheduleSolution BestSolution;
        private Random _rnd = new Random();
        private Dictionary<int, List<int>> _resourcesOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, List<int>> _projectsOperations = new Dictionary<int, List<int>>();
        private Dictionary<int, Dictionary<int, List<int>>> _opsWithOneResInOneProj = new Dictionary<int, Dictionary<int, List<int>>>();
        private ScheduleSolution oldBest;

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
            OpsToProjects();
            //OpsToResources();
            //InitPheromones();
            //CalculateBeginTime(operations);
        }
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
        public ScheduleSolution BuildSollution()
        {
            ScheduleSolution solution = new ScheduleSolution(_operations, _pheromones);
            foreach(var res in  _resourcesOperations)
            {
                var operationsByResource = res.Value;
                while(operationsByResource.Any())
                {

                }
            }
        }
        public void CalculateNextOperation(List<int> operations)
        {
            Dictionary<int, double> probabilities = new Dictionary<int, double>();
            double sum = operations.Sum(op => _operations[op].StartTime);
            foreach(var operation in operations)
            {
                double F = (_operations[].StartTime > operationsCopy[pred].StartTime) ? 2 : operationsCopy[after].StartTime == operationsCopy[pred].StartTime ? 1 : 0.5;
                double pheij = _pheromones[(pred, after)];
                double pheji = _pheromones[(after, pred)];
            }
        }
    }
}
