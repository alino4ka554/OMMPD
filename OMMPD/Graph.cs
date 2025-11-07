using System;
using System.Collections.Generic;
using System.Linq;

namespace OMMPD
{
    public class Graph
    {
        private Dictionary<int, List<int>> adj; 
        private int maxVertex; 

        public Graph()
        {
            adj = new Dictionary<int, List<int>>();
            maxVertex = -1;
        }

        public void AddEdge(int v, int w)
        {
            maxVertex = Math.Max(maxVertex, Math.Max(v, w));

            if (!adj.ContainsKey(v))
                adj[v] = new List<int>();

            

            adj[v].Add(w);

            if (!adj.ContainsKey(w))
                adj[w] = new List<int>();
            
            if (adj[w].Contains(v))
                Console.WriteLine("POVTOR!!!!!");
        }

        private void TopologicalSortUtil(int v, Dictionary<int, bool> visited, Stack<int> stack)
        {
            visited[v] = true;

            if (adj.ContainsKey(v))
            {
                foreach (int neighbor in adj[v])
                {
                    if (visited.ContainsKey(neighbor) && !visited[neighbor])
                        TopologicalSortUtil(neighbor, visited, stack);
                }
            }

            stack.Push(v);
        }

        public Stack<int> TopologicalSort()
        {
            if (maxVertex < 0)
            {
                Console.WriteLine("Граф пуст!");
                return null;
            }

            Stack<int> stack = new Stack<int>();
            Dictionary<int, bool> visited = new Dictionary<int, bool>();
            for (int i = 1; i <= maxVertex; i++)
            {
                visited[i] = false;
            }

            for (int i = 1; i <= maxVertex; i++)
            {
                if (!visited[i])
                    TopologicalSortUtil(i, visited, stack);
            }
            /*Console.Write("Топологическая сортировка графа: ");
            while (stack.Count > 0)
            {
                Console.Write(stack.Pop() + " ");
            }
            Console.WriteLine();*/
            return stack;
        }

        public void TopologicalSortOptimized()
        {
            if (adj.Count == 0)
            {
                Console.WriteLine("Граф пуст!");
                return;
            }

            Stack<int> stack = new Stack<int>();
            Dictionary<int, bool> visited = new Dictionary<int, bool>();

            foreach (int vertex in adj.Keys)
            {
                visited[vertex] = false;
            }

            foreach (int vertex in adj.Keys.OrderBy(v => v))
            {
                if (!visited[vertex])
                    TopologicalSortUtil(vertex, visited, stack);
            }

            Console.Write("Топологическая сортировка (только существующие вершины): ");
            while (stack.Count > 0)
            {
                Console.Write(stack.Pop() + " ");
            }
            Console.WriteLine();
        }
        public void PrintGraph()
        {
            Console.WriteLine("Структура графа:");
            foreach (var kvp in adj.OrderBy(x => x.Key))
            {
                Console.Write($"Вершина {kvp.Key}: ");
                foreach (int neighbor in kvp.Value)
                {
                    Console.Write(neighbor + " ");
                }
                Console.WriteLine();
            }
        }
    }
    /*class Program
    {
        static void Main(string[] args)
        {
            // Создаем граф без указания размера
            Graph g = new Graph();
            g.AddEdge(5, 2);
            g.AddEdge(5, 0);
            g.AddEdge(4, 0);
            g.AddEdge(4, 1);
            g.AddEdge(2, 3);
            g.AddEdge(3, 1);

            g.PrintGraph();
            Console.WriteLine("\nТопологическая сортировка данного графа:");
            g.TopologicalSort();
            g.TopologicalSortOptimized();

            // Пример с пропущенными вершинами
            Console.WriteLine("\n--- Пример с пропущенными вершинами ---");
            Graph g2 = new Graph();
            g2.AddEdge(1, 3);
            g2.AddEdge(3, 5);
            g2.AddEdge(0, 2);

            g2.PrintGraph();
            g2.TopologicalSort();
            g2.TopologicalSortOptimized();
        }
    }*/
}