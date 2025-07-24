using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public class PoliticalParty
    {
        public string Name { get; set; }
        public double ShareOfGovernment { get; set; } // 0-1 range
    }

    public enum PolicyType
    {
        Economic,
        Financial,
        Political
    }

    public class Policy
    {
        public string Name { get; set; }
        public PolicyType Type { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }

        public Policy(string name, PolicyType type, string description = "")
        {
            Name = name;
            Type = type;
            Description = description;
            IsActive = true;
        }
    }

    public class Department
    {
        public string Name { get; set; }
        public decimal BudgetAllocation { get; set; }
        // TODO: Add control logic for department actions
    }

    public class Government
    {
        public List<PoliticalParty> Parties { get; set; }
        public List<Policy> Policies { get; set; }
        public List<Department> Departments { get; set; }

        public Government()
        {
            Parties = new List<PoliticalParty>();
            Policies = new List<Policy>();
            Departments = new List<Department>();
            // Default department is a state bank
            Departments.Add(new Department { Name = "State Bank", BudgetAllocation = 0 });
        }

        public void AddPolicy(Policy policy) => Policies.Add(policy);
        public void AddDepartment(Department dept) => Departments.Add(dept);
    }
}
