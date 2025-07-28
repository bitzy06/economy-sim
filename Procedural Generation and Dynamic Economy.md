### **Integration Strategy: Procedural Generation and Dynamic Economy**

Document Version: 1.0  
Date: July 12, 2025

#### **1\. Executive Summary**

This document provides a strategic framework for integrating the existing Economy.cs simulation with the procedural city generation system. The current economic model, while functional, relies on abstract or randomized values for production and growth. The procedural generation system creates detailed physical layouts but lacks a direct connection to the game's core economic loops.

The goal of this integration is to create a **symbiotic feedback loop**:

1. The **procedurally generated city layout** (roads, parcels) will provide the physical constraints and opportunities for the economy.  
2. The **state of the economy** (profits, population needs, demand) will, in turn, drive the future growth and land use of the generated city.

This will replace randomness with emergent, simulation-driven behavior, creating a more plausible, complex, and engaging geopolitical experience.

#### **2\. Core Concept: A Unified Simulation Loop**

The central change is to restructure the main game loop (TimerSim\_Tick in MainGame.cs) to reflect the interplay between the physical city and the abstract economy. Instead of being separate processes, they become sequential stages within a single turn.

The new, unified simulation cycle for each city will be:

1. **Land Value Calculation (New Step):** Before economic activity, the system assesses the *potential* of the land. A new LandValueCalculator will determine the value of each generated Parcel based on:  
   * **Accessibility:** Proximity to the generated road network (primary roads are more valuable).  
   * **Proximity Effects:** Nearness to existing successful factories, commercial centers, or undesirable pollution sources.  
   * **Geographic Factors:** Data from the TerrainData and WaterBodyMap (e.g., waterfront property).  
2. **Land Use Bidding (Replaces LandUseAssigner):** The static LandUseAssigner will be replaced by a dynamic bidding process. The Corporation agents from your Economy.cs will act as the "developer agents" from the research.  
   * Each Corporation will evaluate parcels based on the calculated land value and its own strategic goals (e.g., a mining corporation will bid high on parcels near iron deposits).  
   * The highest bidder wins the parcel and assigns its intended LandUse (Industrial, Commercial, etc.). This makes land acquisition a core part of corporate AI.  
3. **Building Refinement (Replaces BuildingGenerator):** The BuildingRefiner (from EnhancedGenerationPlanner.cs) creates the gameplay-relevant data for the building on the newly acquired parcel. The building's Level, PopulationCapacity, and EconomicOutput are determined by the land use and the value of the land.  
4. **Economic Simulation (Your Economy.cs):** Now, your existing economic simulation runs, but with its random elements **replaced by data from the generated world**:  
   * A Factory's ProductionCapacity is no longer a fixed number but is derived directly from its corresponding Building.Level.  
   * A City's total population growth is constrained by the total PopulationCapacity of all its residential buildings.  
   * The profitability of factories and the income of PopClass groups are influenced by the EconomicOutput of their respective buildings.

This cycle ensures that the economy operates within the physical world we've built, and the world evolves based on the pressures of the economy.

#### **3\. Unifying the Data Models**

To enable this feedback loop, we must connect the data models. The following changes are recommended:

* **StrategyGame.City Class:**  
  * Add a property: public CityDataModel ProceduralData { get; set; }. This links the abstract city object to its physical, generated representation.  
* **StrategyGame.Factory Class:**  
  * Add a property: public Building BuildingData { get; set; }. This links a factory to a specific building on a parcel, allowing its ProductionCapacity to be dynamic.  
* **StrategyGame.Building Class (from Building.cs):**  
  * Ensure this class contains the simulation-critical properties: Level, PopulationCapacity, and EconomicOutput, as outlined in EnhancedGenerationPlanner.cs.

#### **4\. Code-Level Integration Plan**

##### **Step 1: Modify Factory.Produce()**

The Produce method in Economy.cs is a primary integration point. It should be modified to use the building's level.

**Current Economy.cs:**

public void Produce(Dictionary\<string, Good\> cityStockpile, City city)  
{  
    // ... logic ...  
    // Checks for inputs based on this.ProductionCapacity  
    // ...  
    // Adds output to stockpile based on this.ProductionCapacity  
}

**Proposed Economy.cs:**

public void Produce(Dictionary\<string, Good\> cityStockpile, City city)  
{  
    // If the factory isn't linked to a physical building, it can't produce.  
    if (this.BuildingData \== null) return;

    // The production capacity is now dynamic, based on the building's level.  
    int currentProductionCapacity \= this.BuildingData.Level;

    // ... logic ...  
    // Checks for inputs based on 'currentProductionCapacity'  
    // ...  
    // Adds output to stockpile based on 'currentProductionCapacity'  
}

##### **Step 2: Modify City.SimulateGrowth()**

Population growth should be constrained by the available housing generated by the procedural system.

**Current Economy.cs:**

public void SimulateGrowth()  
{  
    // Currently simulates growth abstractly.  
}

**Proposed Economy.cs:**

public void SimulateGrowth()  
{  
    // Calculate the total housing capacity from all residential buildings.  
    int maxPopulation \= this.ProceduralData.Buildings  
        .Where(b \=\> b.LandUse \== LandUseType.Residential)  
        .Sum(b \=\> b.PopulationCapacity);

    int currentPopulation \= this.PopClasses.Sum(p \=\> p.Size);

    // Only allow growth if there is available housing capacity.  
    if (currentPopulation \< maxPopulation)  
    {  
        // ... existing growth logic can run here ...  
    }

    // Optional: Add logic for negative effects (emigration, unhappiness)  
    // if currentPopulation \> maxPopulation (overcrowding).  
}

##### **Step 3: Restructure the Main Simulation Loop**

The TimerSim\_Tick in MainGame.cs needs to be re-ordered to follow the new unified sequence.

**Conceptual MainGame.cs Restructure:**

private void TimerSim\_Tick(object sender, EventArgs e)  
{  
    // For each city in the world...  
    foreach (var city in allCitiesInWorld)  
    {  
        // STAGE 1 & 2: Land Value and Bidding  
        // This is where corporations would evaluate and acquire new parcels.  
        var landValueMap \= LandValueCalculator.Calculate(city);  
        LandUseSimulator.RunDeveloperAgentSimulation(city, landValueMap); // Corporations from Economy.cs act as agents.

        // STAGE 3: Building Refinement  
        // Generate the gameplay stats for buildings based on the new land use.  
        BuildingRefiner.RefineBuildings(city, landValueMap);

        // STAGE 4: Your Economic Simulation  
        // Run the existing, but now modified, economy update.  
        Economy.UpdateCityEconomy(city); // This now uses building levels and pop capacity.  
    }

    // ... Run state and country level updates afterwards ...  
    Economy.UpdateStateEconomy(...);  
    Economy.UpdateCountryEconomy(...);

    // ... Refresh UI ...  
}

#### **5\. Phased Implementation Roadmap**

1. **Phase 1: Data Model & One-Way Integration.**  
   * Update the City, Factory, and Building classes to link them.  
   * Implement the BuildingRefiner to generate stats.  
   * Modify Economy.cs (Produce, SimulateGrowth) to *read* data from the procedural system. At this stage, land use can still be assigned by the old LandUseAssigner. This establishes the first half of the feedback loop.  
2. **Phase 2: Dynamic Land Value.**  
   * Create the LandValueCalculator. This module will be the "brain" that assesses the potential of the generated landscape.  
3. **Phase 3: Full Feedback Loop.**  
   * Replace the static LandUseAssigner with the LandUseSimulator.  
   * Modify Corporation.UpdateAI to include logic for evaluating and bidding on parcels based on the land value map.  
   * Restructure the main game loop as described above.

By following this strategy, your economy will cease to be an abstract layer and will become fully grounded in the procedurally generated world, creating a far more robust and emergent simulation.