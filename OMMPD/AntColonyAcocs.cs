using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace OMMPD
{
    // Версия ACO для RCPSP с инкрементальным апдейтом StartTime и локальным поиском
    public class AntColonyAco
    {
        // Параметры алгоритма
        private int _iterations;
        private int _ants;
        private double _alpha; // важность феромона
        private double _beta;  // важность эвристики (eta)
        private double _rho;   // скорость испарения
        private double _tauMin;
        private double _tauMax;
        private double _Q;     // масштаб для депозита

        // Исходные операции (не меняем)
        private readonly Dictionary<int, Operation> _initialOperations;
        private readonly List<Operation> _initialList;
        private Dictionary<int, List<int>> Resources = new Dictionary<int, List<int>>();
        // Феромоны и вероятности
        public Dictionary<(int, int), double> _pheromones;
        public Dictionary<(int, int), double> _probabilities;

        public ScheduleSolution GlobalBest; // глобально лучший
        private Random _rnd = new Random();

        public AntColonyAco(
            List<Operation> operations,
            int iterations = 200,
            int ants = 50,
            double alpha = 1.0,
            double beta = 2.0,
            double rho = 0.1,
            double tauMin = 1e-4,
            double tauMax = 5.0,
            double Q = 1000.0)
        {
            _iterations = iterations;
            _ants = ants;
            _alpha = alpha;
            _beta = beta;
            _rho = rho;
            _tauMin = tauMin;
            _tauMax = tauMax;
            _Q = Q;

            // сохраним исходные (immutable)
            _initialList = operations;
            _initialOperations = operations.ToDictionary(o => o.Id, o => o.CloneOriginal());

            InitPheromones();
            ResourceInit();
        }
        public void ResourceInit()
        {
            Resources = _initialOperations.Values.GroupBy(o => o.Resource)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        }
        // Инициализация феромонов: пары операций, конкурирующих по ресурсам и не имеющих предопределённой зависимости
        private void InitPheromones()
        {
            _pheromones = new Dictionary<(int, int), double>();
            _probabilities = new Dictionary<(int, int), double>();

            foreach (var i in _initialOperations.Values)
            {
                foreach (var j in _initialOperations.Values)
                {
                    if ((i != j) && i.Resource == j.Resource && !i.DependsOn.Contains(j.Id) && !j.DependsOn.Contains(i.Id) && (i.Project != j.Project))
                    {
                        _pheromones.Add((i.Id, j.Id), _tauMax);
                    }
                }
            }
        }

        // Запуск алгоритма
        public void Run()
        {
            Console.WriteLine($"ACO start: iterations={_iterations}, ants={_ants}, alpha={_alpha}, beta={_beta}, rho={_rho}");
            CalculateBeginTime(_initialOperations);
            for (int iter = 0; iter < _iterations; iter++)
            {
                ScheduleSolution iterationBest = null;

                for (int k = 0; k < _ants; k++)
                {
                    // 1) Построить решение (локальная копия операций)
                    var sol = BuildSolution();

                    // 2) Локальный поиск (улучшить решение)
                    //LocalSearchImprove(sol);

                    // 3) Оценить
                    CalculateTotalTime(sol);

                    // 4) Запомнить лучший за итерацию
                    if (iterationBest == null || sol.TotalTime < iterationBest.TotalTime)
                        iterationBest = sol;

                    // 5) Обновить глобальный лучший
                    if (GlobalBest == null || sol.TotalTime < GlobalBest.TotalTime)
                        GlobalBest = sol;
                }

                // Обновление феромонов: испарение + депозит по iterationBest + elitist deposit по GlobalBest
                UpdatePheromones(iterationBest, GlobalBest);

                Console.WriteLine($"Iter {iter + 1}/{_iterations}: iterBest = {iterationBest.TotalTime}, globalBest = {GlobalBest.TotalTime}");
            }

            Console.WriteLine("ACO finished. Global best time = " + GlobalBest.TotalTime);
        }
        private ScheduleSolution BuildSolution()
        {
            var ops = _initialList.Select(o => (Operation)o.Clone()).ToDictionary(o => o.Id, o => o);
            foreach(var resource in Resources.Keys)
            {
                var list = Resources[resource];
                foreach(var i in list)
                {
                    foreach(var j in list)
                    {
                        if(i != j && !ops[i].DependsOn.Contains(j) && !ops[j].DependsOn.Contains(i) && ops[i].Project != ops[j].Project)
                        {
                            double F = (ops[j].StartTime > ops[i].StartTime) ? 2 : ops[j].StartTime == ops[i].StartTime ? 1 : 0.5;
                            double pheij = _pheromones[(i, j)];
                            double pheji = _pheromones[(j, i)];
                            _probabilities[(i, j)] = (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha))
                                / (Math.Pow(F, _beta) * Math.Pow(pheij, _alpha)
                                + Math.Pow(1 / F, _beta) * Math.Pow(pheji, _alpha));
                            if (_rnd.NextDouble() <= _probabilities[(i, j)])
                            {
                                ops[j].DependsOn.Add(i);
                            }
                            else
                            {
                                ops[i].DependsOn.Add(j);
                            }
                            CalculateBeginTime(ops);
                        }
                    }
                }
                
            }
            return new ScheduleSolution(ops, _pheromones);
        }

        private void CalculateBeginTime(Dictionary<int, Operation> ops)
        {
            foreach (var op in ops.Values)
                op.StartTime = -1;
            foreach (var op in ops.Values)
            {
                CalculateBeginPred(op, ops);
            }
        }
        private void CalculateBeginPred(Operation op, Dictionary<int, Operation> ops)
        {
            if (op.DependsOn.Count() == 0)
                op.StartTime = 0;
            else 
            {
                var time = 0.0;
                foreach (var pred in op.DependsOn)
                {
                    if (ops[pred].StartTime != -1)
                    {
                        if (time < ops[pred].StartTime + ops[pred].NormalTime)
                            time = ops[pred].StartTime + ops[pred].NormalTime;
                        if ((GlobalBest != null && GlobalBest.TotalTime < time) || time > 1000)
                            return;
                    }
                    else
                        CalculateBeginPred(ops[pred], ops);
                }
                op.StartTime = time;
            }
        }
        // Построение решения одним муравьём
        private ScheduleSolution BuildSolutionSingleAnt()
        {
            // клонируем исходные операции (без StartTime)
            var operations = _initialList.Select(o => (Operation)o.Clone()).ToDictionary(o => o.Id, o => o);

            foreach (var op in operations.Values)
                op.Successors.Clear();

            foreach (var op in operations.Values)
            {
                foreach (int pred in op.DependsOn)
                {
                    operations[pred].Successors.Add(op.Id);
                }
            }

            // Build successors from DependsOn
            foreach (var op in operations.Values)
            {
                foreach (var p in op.DependsOn)
                {
                    if (!operations.ContainsKey(p)) continue;
                    if (!operations[p].Successors.Contains(op.Id)) operations[p].Successors.Add(op.Id);
                }
            }

            // назначения StartTime для корней
            foreach (var op in operations.Values)
                if (!op.DependsOn.Any())
                    op.StartTime = 0;

            var solution = new ScheduleSolution(operations, _pheromones);

            // Группируем по ресурсам candidate lists
            var byResource = operations.Values.GroupBy(o => o.Resource)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            // Для каждой пары на одном ресурсе решаем их относительный порядок.
            // Явный парный перебор (i,j) — простой вариант; можно улучшить candidate set.
            foreach (var res in byResource.Keys)
            {
                var list = byResource[res];
                // порядок прохода важен: можно сортировать по приоритету/длительности
                list = list.OrderBy(id => operations[id].Priority).ToList();

                for (int a = 0; a < list.Count; a++)
                {
                    for (int b = a + 1; b < list.Count; b++)
                    {
                        int i = list[a], j = list[b];

                        // если уже есть зависимость — пропускаем
                        if (operations[i].DependsOn.Contains(j) || operations[j].DependsOn.Contains(i))
                            continue;

                        var keyIJ = (i, j);
                        var keyJI = (j, i);
                        if (!_pheromones.ContainsKey(keyIJ) || !_pheromones.ContainsKey(keyJI))
                        {
                            // если пара не инициализирована (редко), инициализируем
                            if (!_pheromones.ContainsKey(keyIJ)) _pheromones[keyIJ] = _tauMax;
                            if (!_pheromones.ContainsKey(keyJI)) _pheromones[keyJI] = _tauMax;
                        }

                        // вычисляем вероятность, используя локальное состояние operations
                        double prob = ComputeProbForPair(operations, i, j);

                        // делаем случайный выбор
                        if (_rnd.NextDouble() <= prob)
                        {
                            // хотим i -> j (j после i)
                            if (!WouldCreateCycle(operations, i, j))
                            {
                                operations[j].DependsOn.Add(i);
                                operations[i].Successors.Add(j);
                                UpdateStartTimesAfterAddingEdge(operations, j);
                                solution.W[(i, j)] = 1;
                            }
                            else
                            {
                                // если цикл — вынужденно делаем обратное
                                operations[i].DependsOn.Add(j);
                                operations[j].Successors.Add(i);
                                UpdateStartTimesAfterAddingEdge(operations, i);
                                solution.W[(j, i)] = 1;
                            }
                        }
                        else
                        {
                            // хотим j -> i
                            if (!WouldCreateCycle(operations, j, i))
                            {
                                operations[i].DependsOn.Add(j);
                                operations[j].Successors.Add(i);
                                UpdateStartTimesAfterAddingEdge(operations, i);
                                solution.W[(j, i)] = 1;
                            }
                            else
                            {
                                operations[j].DependsOn.Add(i);
                                operations[i].Successors.Add(j);
                                UpdateStartTimesAfterAddingEdge(operations, j);
                                solution.W[(i, j)] = 1;
                            }
                        }
                    }
                }
            }

            // В конце гарантируем, что все StartTime назначены (если остались -1 — сделаем полную топологию)
            EnsureAllStartTimes(operations);

            solution.Operations = operations;
            return solution;
        }

        // Вычисление вероятности между i и j на базе локального состояния
        private double ComputeProbForPair(Dictionary<int, Operation> ops, int i, int j)
        {
            var keyIJ = (i, j);
            var keyJI = (j, i);
            double tau_ij = _pheromones.ContainsKey(keyIJ) ? _pheromones[keyIJ] : _tauMin;
            double tau_ji = _pheromones.ContainsKey(keyJI) ? _pheromones[keyJI] : _tauMin;

            // эвристика eta: короткая операция предпочтительней (можно менять)
            double eta_ij = 1.0 / (1.0 + ops[j].NormalTime);
            double eta_ji = 1.0 / (1.0 + ops[i].NormalTime);

            double num = Math.Pow(tau_ij, _alpha) * Math.Pow(eta_ij, _beta);
            double denom = num + Math.Pow(tau_ji, _alpha) * Math.Pow(eta_ji, _beta);
            if (denom <= 0) return 0.5;
            return num / denom;
        }

        // Проверка, создаст ли добавление ребра from -> to цикл (по Successors)
        private bool WouldCreateCycle(Dictionary<int, Operation> ops, int from, int to)
        {
            var stack = new Stack<int>();
            var visited = new HashSet<int>();
            stack.Push(to); // ищем путь to -> ... -> from (потомки)
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == from) return true;
                if (!visited.Add(cur)) continue;
                foreach (var succ in ops[cur].Successors)
                    if (!visited.Contains(succ))
                        stack.Push(succ);
            }
            return false;
        }

        // Инкрементальное обновление StartTime после добавления ребра пред->to
        private void UpdateStartTimesAfterAddingEdge(Dictionary<int, Operation> ops, int to)
        {
            // новый старт для to = max(ends of preds)
            double newStart = 0;
            foreach (var pred in ops[to].DependsOn)
            {
                double predStart = ops[pred].StartTime;
                // если пред ещё не имел StartTime, задаём 0 (или можно рекурсивно вычислить)
                if (predStart < 0) predStart = 0;
                double predEnd = predStart + ops[pred].NormalTime;
                if (predEnd > newStart) newStart = predEnd;
            }

            if (newStart <= ops[to].StartTime) return;
            ops[to].StartTime = newStart;

            // BFS по потомкам: обновляем только если увеличилось
            var q = new Queue<int>();
            q.Enqueue(to);
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var succ in ops[cur].Successors)
                {
                    double succNewStart = 0;
                    foreach (var pred in ops[succ].DependsOn)
                    {
                        double predEnd = ops[pred].StartTime + ops[pred].NormalTime;
                        if (predEnd > succNewStart) succNewStart = predEnd;
                    }
                    if (succNewStart > ops[succ].StartTime)
                    {
                        ops[succ].StartTime = succNewStart;
                        q.Enqueue(succ);
                    }
                }
            }
        }

        // Если остались незаполненные StartTime (-1), выполняем полную топологию и заполняем их.
        private void EnsureAllStartTimes(Dictionary<int, Operation> ops)
        {
            var unoriented = ops.Values.Where(o => o.StartTime < 0).ToList();
            if (!unoriented.Any()) return;

            // простая топологическая сортировка + вычисление
            var graph = new Graph();
            foreach (var op in ops.Values)
                foreach (var pred in op.DependsOn)
                    graph.AddEdge(pred, op.Id);

            var stack = graph.TopologicalSort(); // стек, вершины в порядке postorder
            if (stack == null) return;
            var list = stack.Reverse().ToList(); // правильный топологический порядок
            foreach (var id in list)
            {
                var op = ops[id];
                if (!op.DependsOn.Any())
                {
                    if (op.StartTime < 0) op.StartTime = 0;
                }
                else
                {
                    double start = 0;
                    foreach (var pred in op.DependsOn)
                    {
                        double predEnd = ops[pred].StartTime + ops[pred].NormalTime;
                        if (predEnd > start) start = predEnd;
                    }
                    op.StartTime = start;
                }
            }
        }

        // Локальный поиск: left-shift (задвинуть влево) по каждому ресурсу и простые swap улучшения
        private void LocalSearchImprove(ScheduleSolution sol)
        {
            var ops = sol.Operations;

            // candidate sequences by resource (ordered by current start)
            var byResource = ops.Values
                .GroupBy(o => o.Resource)
                .ToDictionary(g => g.Key, g => g.OrderBy(id => id.StartTime).Select(o => o.Id).ToList());

            // Left-shift: для каждого ресурса попытаться сдвинуть каждую задачу максимально влево (respect precedence)
            foreach (var kv in byResource)
            {
                var seq = kv.Value;
                foreach (var id in seq)
                {
                    var op = ops[id];
                    // earliest allowed = max(end of preds, end of previous on same resource)
                    double earliest = 0;
                    if (op.DependsOn.Any())
                        earliest = op.DependsOn.Max(p => ops[p].StartTime + ops[p].NormalTime);

                    // previous on resource
                    var index = seq.IndexOf(id);
                    if (index > 0)
                    {
                        var prev = ops[seq[index - 1]];
                        earliest = Math.Max(earliest, prev.StartTime + prev.NormalTime);
                    }
                    if (earliest < op.StartTime)
                    {
                        op.StartTime = earliest;
                        // update successors cascade
                        UpdateStartTimesAfterAddingEdge(ops, id);
                    }
                }
            }

            // Простые swap: по каждому ресурсу пробуем перестановки соседних пар и принимаем улучшение
            foreach (var kv in byResource)
            {
                var seq = kv.Value.ToList();
                bool improved;
                int iter = 0;
                do
                {
                    improved = false;
                    for (int s = 0; s < seq.Count - 1; s++)
                    {
                        int a = seq[s], b = seq[s + 1];

                        // пропускаем, если зависимости запрещают swap
                        if (ops[a].DependsOn.Contains(b) || ops[b].DependsOn.Contains(a)) continue;

                        // попробуем swap: создадим копию времен, поменяем и пересчитаем локально
                        var backup = new Dictionary<int, double>();
                        foreach (var id in seq) backup[id] = ops[id].StartTime;

                        // временно поменяем их очередность: сделаем b перед a
                        // убираем существующие отношения на этом ресурсе и добавляем обратные
                        // проще: попросим Compute schedule with swapped order:
                        // для простоты выполняем brute force small recalculation:
                        // set a.start = max(preds, end of previous before b); set b.start accordingly
                        // Implement a safe attempt: recompute full schedule for ops on this resource
                        var newOrder = seq.ToList();
                        newOrder[s] = b;
                        newOrder[s + 1] = a;

                        // Apply newOrder: recompute start times for all tasks on this resource using current preds
                        double prevEnd = 0;
                        bool feasible = true;
                        foreach (var id in newOrder)
                        {
                            var opcur = ops[id];
                            double earliest = 0;
                            if (opcur.DependsOn.Any()) earliest = opcur.DependsOn.Max(p => ops[p].StartTime + ops[p].NormalTime);
                            earliest = Math.Max(earliest, prevEnd);
                            // check that earliest doesn't violate dependencies (shouldn't)
                            if (earliest < 0) earliest = 0;
                            prevEnd = earliest + opcur.NormalTime;
                        }

                        // Recompute entire StartTimes for affected nodes (simple approach: call EnsureAllStartTimes)
                        // Backup entire starts
                        var startsBackup = ops.Keys.ToDictionary(id => id, id => ops[id].StartTime);

                        // Try naive: assign new order start times
                        prevEnd = 0;
                        foreach (var id in newOrder)
                        {
                            var opcur = ops[id];
                            double earliest = 0;
                            if (opcur.DependsOn.Any()) earliest = opcur.DependsOn.Max(p => ops[p].StartTime + ops[p].NormalTime);
                            earliest = Math.Max(earliest, prevEnd);
                            ops[id].StartTime = earliest;
                            prevEnd = earliest + opcur.NormalTime;
                        }
                        EnsureAllStartTimes(ops);

                        // evaluate total time
                        double newTotal = ops.Values.Max(o => o.StartTime + o.NormalTime);
                        double oldTotal = startsBackup.Values.Max(sv => sv + ops[ ops.Keys.First(k=>ops[k].StartTime==sv) ].NormalTime); // rough

                        // If improved - keep, otherwise rollback
                        double globalNew = ops.Values.Max(o => o.StartTime + o.NormalTime);
                        // Evaluate quickly: if globalNew < current known (sol.TotalTime) => improvement
                        if (globalNew + 1e-9 < sol.TotalTime)
                        {
                            // accepted
                            seq = newOrder;
                            improved = true;
                            sol.TotalTime = globalNew;
                            break;
                        }
                        else
                        {
                            // rollback
                            foreach (var id in startsBackup.Keys) ops[id].StartTime = startsBackup[id];
                        }
                    }
                    iter++;
                } while (improved && iter < 5);
            }

            // After local search recalc total time
            CalculateTotalTime(sol);
        }

        // Вычисление totalTime
        private void CalculateTotalTime(ScheduleSolution sol)
        {
            double total = 0;
            foreach (var op in sol.Operations.Values)
            {
                double end = op.StartTime + op.NormalTime;
                if (end > total) total = end;
            }
            sol.TotalTime = total;
        }

        // Обновление феромонов: испарение, депозит для iterationBest и elitist депозит для globalBest
        private void UpdatePheromones(ScheduleSolution iterationBest, ScheduleSolution globalBest)
        {
            // испарение
            foreach (var key in _pheromones.Keys.ToList())
                _pheromones[key] = Math.Max(_pheromones[key] * (1 - _rho), _tauMin);

            if (iterationBest != null)
            {
                double delta = _Q / Math.Max(1.0, iterationBest.TotalTime);
                foreach (var kv in iterationBest.W)
                {
                    if (kv.Value == 1)
                    {
                        _pheromones[kv.Key] = Math.Min(_pheromones[kv.Key] + delta, _tauMax);
                    }
                }
            }

            if (globalBest != null)
            {
                double elitDelta = 3.0 * _Q / Math.Max(1.0, globalBest.TotalTime);
                foreach (var kv in globalBest.W)
                {
                    if (kv.Value == 1)
                        _pheromones[kv.Key] = Math.Min(_pheromones[kv.Key] + elitDelta, _tauMax);
                }
            }
        }
    }
}
