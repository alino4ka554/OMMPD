using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OMMPD
{
    public class Operation : ICloneable
    {
        public int Id { get; set; }
        public List<int> DependsOn { get; set; } = new List<int>();
        public int Project { get; set; }
        public int Resource { get; set; }
        public double NormalTime { get; set; }
        public double CrashTime { get; set; }
        public double NormalCost { get; set; }
        public double CrashCost { get; set; }
        public double StartTime { get; set; }
        public double Acceleration { get; set; }
        public bool IsRunning { get; set; }
        public int Priority = 1;
        public List<int> Successors { get; set; } = new List<int>();
        public double ActualTime => Math.Max(NormalTime - Acceleration, CrashTime);
        public double ActualCost => NormalCost + Delta * Acceleration;
        public double Delta => (CrashCost - NormalCost) / (NormalTime - CrashTime);
        public bool Is = false;

        public double EndTime => StartTime + ActualTime;
        public object Clone()
        {
            return new Operation
            {
                Id = this.Id,
                Project = this.Project,
                Resource = this.Resource,
                NormalTime = this.NormalTime,
                CrashTime = this.CrashTime,
                NormalCost = this.NormalCost,
                CrashCost = this.CrashCost,
                Priority = this.Priority,
                Acceleration = this.Acceleration,
                StartTime = 0,
                DependsOn = new List<int>(this.DependsOn)
            };
        }
        public Operation CloneOriginal()
        {
            return new Operation
            {
                Id = this.Id,
                Project = this.Project,
                Resource = this.Resource,
                NormalTime = this.NormalTime,
                CrashTime = this.CrashTime,
                NormalCost = this.NormalCost,
                CrashCost = this.CrashCost,
                Priority = this.Priority,
                Acceleration = this.Acceleration,
                StartTime = this.StartTime,
                DependsOn = new List<int>(this.DependsOn),
                Successors = new List<int>(this.Successors)
            };
        }
    }
}
