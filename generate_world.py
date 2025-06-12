import json
import random

# Define real-world data structure
# You can expand this list with more countries, states, and cities.
real_world_template = [
    {
        "Name": "United States of America",
        "States": [
            {"Name": "New York", "Cities": ["New York City", "Buffalo", "Albany"]},
            {"Name": "California", "Cities": ["Los Angeles", "San Francisco", "San Diego"]},
            {"Name": "Texas", "Cities": ["Houston", "Dallas", "Austin"]},
            {"Name": "Illinois", "Cities": ["Chicago", "Springfield", "Peoria"]},
            {"Name": "Pennsylvania", "Cities": ["Philadelphia", "Pittsburgh", "Harrisburg"]} # Changed Florida to Pennsylvania for variety
        ]
    },
    {
        "Name": "German Empire", # Historical context for the game's era
        "States": [
            {"Name": "Prussia", "Cities": ["Berlin", "Konigsberg", "Danzig"]},
            {"Name": "Bavaria", "Cities": ["Munich", "Nuremberg", "Augsburg"]},
            {"Name": "Saxony", "Cities": ["Dresden", "Leipzig", "Chemnitz"]},
            {"Name": "Württemberg", "Cities": ["Stuttgart", "Ulm", "Heilbronn"]},
            {"Name": "Alsace-Lorraine", "Cities": ["Strasbourg", "Metz", "Mulhouse"]} # Changed Baden
        ]
    },
    {
        "Name": "Empire of Japan", # Historical context
        "States": [
            {"Name": "Kanto", "Cities": ["Tokyo", "Yokohama", "Chiba"]},
            {"Name": "Kansai", "Cities": ["Osaka", "Kyoto", "Kobe"]},
            {"Name": "Kyushu", "Cities": ["Fukuoka", "Nagasaki", "Kumamoto"]},
            {"Name": "Hokkaido", "Cities": ["Sapporo", "Hakodate", "Asahikawa"]},
            {"Name": "Chugoku", "Cities": ["Hiroshima", "Okayama", "Yamaguchi"]} # Changed Tohoku
        ]
    },
    {
        "Name": "Republic of Brazil", # Historical context
        "States": [
            {"Name": "Rio de Janeiro State", "Cities": ["Rio de Janeiro City", "Niterói", "Petrópolis"]}, # Clarified State vs City
            {"Name": "São Paulo State", "Cities": ["São Paulo City", "Campinas", "Santos"]}, # Clarified
            {"Name": "Minas Gerais", "Cities": ["Belo Horizonte", "Ouro Preto", "Juiz de Fora"]},
            {"Name": "Bahia", "Cities": ["Salvador", "Feira de Santana", "Vitória da Conquista"]},
            {"Name": "Pernambuco", "Cities": ["Recife", "Olinda", "Caruaru"]} # Changed Rio Grande do Sul
        ]
    },
    {
        "Name": "United Kingdom",
        "States": [
            {"Name": "England", "Cities": ["London", "Manchester", "Birmingham"]},
            {"Name": "Scotland", "Cities": ["Edinburgh", "Glasgow", "Aberdeen"]},
            {"Name": "Wales", "Cities": ["Cardiff", "Swansea", "Newport"]},
            {"Name": "Ireland (Whole Island)", "Cities": ["Dublin", "Belfast", "Cork"]}, # Simplified for era
            {"Name": "Northumbria", "Cities": ["Newcastle", "Sunderland", "Durham"]}
        ]
    },
    {
        "Name": "French Republic",
        "States": [
            {"Name": "Île-de-France", "Cities": ["Paris", "Versailles", "Saint-Denis"]},
            {"Name": "Provence-Alpes-Côte d'Azur", "Cities": ["Marseille", "Nice", "Toulon"]},
            {"Name": "Occitanie", "Cities": ["Toulouse", "Montpellier", "Nîmes"]},
            {"Name": "Auvergne-Rhône-Alpes", "Cities": ["Lyon", "Grenoble", "Saint-Étienne"]},
            {"Name": "Brittany", "Cities": ["Rennes", "Brest", "Nantes"]} # Nantes technically Pays de la Loire, but historically linked
        ]
    },
    {
        "Name": "Russian Empire",
        "States": [
            {"Name": "Moscow Governorate", "Cities": ["Moscow", "Tver", "Ryazan"]},
            {"Name": "Saint Petersburg Governorate", "Cities": ["Saint Petersburg", "Novgorod", "Pskov"]},
            {"Name": "Kiev Governorate", "Cities": ["Kiev", "Chernigov", "Poltava"]}, # Modern Ukraine
            {"Name": "Siberia General Governorate", "Cities": ["Irkutsk", "Omsk", "Tomsk"]},
            {"Name": "Caucasus Viceroyalty", "Cities": ["Tiflis (Tbilisi)", "Baku", "Yerevan"]}
        ]
    },
    {
        "Name": "Austro-Hungarian Empire",
        "States": [
            {"Name": "Austria Proper", "Cities": ["Vienna", "Linz", "Salzburg"]},
            {"Name": "Kingdom of Hungary", "Cities": ["Budapest", "Debrecen", "Szeged"]},
            {"Name": "Kingdom of Bohemia", "Cities": ["Prague", "Brno", "Pilsen"]},
            {"Name": "Galicia and Lodomeria", "Cities": ["Lviv (Lemberg)", "Krakow", "Ternopil"]},
            {"Name": "Croatia-Slavonia", "Cities": ["Zagreb (Agram)", "Osijek", "Rijeka (Fiume)"]}
        ]
    },
    {
        "Name": "Ottoman Empire",
        "States": [
            {"Name": "Constantinople Vilayet", "Cities": ["Constantinople (Istanbul)", "Adrianople (Edirne)", "Gallipoli"]},
            {"Name": "Anatolia Vilayet", "Cities": ["Smyrna (Izmir)", "Ankara", "Konya"]},
            {"Name": "Syria Vilayet", "Cities": ["Damascus", "Beirut", "Aleppo"]},
            {"Name": "Egypt Khedivate", "Cities": ["Cairo", "Alexandria", "Port Said"]}, # Technically autonomous but part of empire
            {"Name": "Rumelia Eyalet", "Cities": ["Thessaloniki (Salonica)", "Monastir (Bitola)", "Skopje (Üsküp)"]}
        ]
    },
    {
        "Name": "Kingdom of Italy",
        "States": [
            {"Name": "Piedmont", "Cities": ["Turin", "Alessandria", "Novara"]},
            {"Name": "Lombardy", "Cities": ["Milan", "Brescia", "Bergamo"]},
            {"Name": "Kingdom of Naples (Two Sicilies)", "Cities": ["Naples", "Palermo", "Bari"]}, # Historical region
            {"Name": "Tuscany", "Cities": ["Florence", "Pisa", "Siena"]},
            {"Name": "Venetia", "Cities": ["Venice", "Verona", "Padua"]}
        ]
    },
    {
        "Name": "Qing China",
        "States": [
            {"Name": "Zhili Province", "Cities": ["Peking (Beijing)", "Tianjin", "Baoding"]},
            {"Name": "Jiangsu Province", "Cities": ["Nanking (Nanjing)", "Shanghai", "Suzhou"]},
            {"Name": "Guangdong Province", "Cities": ["Canton (Guangzhou)", "Shenzhen", "Foshan"]},
            {"Name": "Sichuan Province", "Cities": ["Chengdu", "Chongqing", "Zigong"]},
            {"Name": "Shandong Province", "Cities": ["Jinan", "Qingdao", "Yantai"]}
        ]
    },
    {
        "Name": "Kingdom of Spain",
        "States": [
            {"Name": "New Castile", "Cities": ["Madrid", "Toledo", "Guadalajara"]},
            {"Name": "Old Castile and León", "Cities": ["Valladolid", "Burgos", "Salamanca"]},
            {"Name": "Catalonia", "Cities": ["Barcelona", "Tarragona", "Girona"]},
            {"Name": "Andalusia", "Cities": ["Seville", "Málaga", "Granada"]},
            {"Name": "Aragon", "Cities": ["Zaragoza", "Huesca", "Teruel"]}
        ]
    },
    {
        "Name": "Dominion of Canada",
        "States": [
            {"Name": "Ontario", "Cities": ["Toronto", "Ottawa", "Hamilton"]},
            {"Name": "Quebec", "Cities": ["Montreal", "Quebec City", "Sherbrooke"]},
            {"Name": "Nova Scotia", "Cities": ["Halifax", "Sydney", "Dartmouth"]},
            {"Name": "British Columbia", "Cities": ["Victoria", "Vancouver", "New Westminster"]},
            {"Name": "Manitoba", "Cities": ["Winnipeg", "Brandon", "Portage la Prairie"]}
        ]
    },
    {
        "Name": "Argentine Republic",
        "States": [
            {"Name": "Buenos Aires Province", "Cities": ["Buenos Aires City", "La Plata", "Mar del Plata"]},
            {"Name": "Córdoba Province", "Cities": ["Córdoba City", "Río Cuarto", "Villa María"]},
            {"Name": "Santa Fe Province", "Cities": ["Rosario", "Santa Fe City", "Rafaela"]},
            {"Name": "Mendoza Province", "Cities": ["Mendoza City", "San Rafael", "Godoy Cruz"]},
            {"Name": "Tucumán Province", "Cities": ["San Miguel de Tucumán", "Yerba Buena", "Tafí Viejo"]}
        ]
    },
    {
        "Name": "United Mexican States", # Official name
        "States": [
            {"Name": "Federal District", "Cities": ["Mexico City", "Xochimilco", "Tlalpan"]},
            {"Name": "Jalisco", "Cities": ["Guadalajara", "Zapopan", "Tlaquepaque"]},
            {"Name": "Nuevo León", "Cities": ["Monterrey", "Guadalupe", "Apodaca"]},
            {"Name": "Veracruz", "Cities": ["Veracruz City", "Xalapa", "Coatzacoalcos"]},
            {"Name": "Puebla", "Cities": ["Puebla City", "Tehuacán", "Cholula"]}
        ]
    }
]

def generate_world_data(num_countries=10, num_states_per_country=5, num_cities_per_state_min=3): # Renamed num_cities_per_state to num_cities_per_state_min
    world = {"Countries": []}

    # Removed old random name part lists to avoid confusion if not used.
    # If you want hybrid generation (some real, some random), these could be reinstated.

    available_factory_blueprints = [
        "Grain Farm", "Coal Mine", "Iron Mine", "Cotton Plantation", "Logging Camp", "Oil Derrick",
        "Rubber Plantation", "Fishing Wharf", "Cattle Ranch", "Tea Plantation", "Coffee Plantation",
        "Tobacco Plantation", "Sugar Plantation", "Copper Mine", "Tin Mine", "Lead Mine", "Zinc Mine",
        "Limestone Quarry", "Salt Mine", "Sulphur Mine", "Dye Collection Post",
        "Steel Mill", "Sawmill", "Textile Mill", "Paper Mill", "Oil Refinery", "Rubber Processor",
        "Copper Smelter", "Tin Smelter", "Lead Smelter", "Zinc Smelter", "Bronze Foundry", "Brass Foundry",
        "Chemicals Plant", "Explosives Factory", "Fertilizer Plant", "Cement Plant",
        "Tool Factory", "Machine Parts Factory",
        "Bakery", "Cannery", "Meat Packing Plant", "Tea Factory", "Coffee Roastery", "Sugar Mill",
        "Clothing Factory", "Furniture Factory", "Printing Press", "Luxury Tailor", "Tobacco Factory",
        "Automobile Plant", "Electronics Plant",
        "Arms Factory", "Munitions Plant", "Artillery Plant"
    ]

    # These sets are still useful if using random elements or to ensure no factory name clashes if we add random factories later
    used_country_names_output = set()
    used_state_names_global_output = set()
    used_city_names_global_output = set()

    for i in range(num_countries):
        country_template = real_world_template[i % len(real_world_template)] # Cycle through templates
        
        country_name = country_template["Name"]
        # Simple way to avoid duplicate country names if num_countries > len(real_world_template)
        instance_count = 1
        original_country_name = country_name
        while country_name in used_country_names_output:
            instance_count += 1
            country_name = f"{original_country_name} ({instance_count})"
        used_country_names_output.add(country_name)

        country_data = {
            "Name": country_name,
            "TaxRate": round(random.uniform(0.05, 0.25), 2),
            "NationalExpenses": random.randint(500000, 20000000),
            "InitialPopulation": 0,
            "InitialBudget": random.randint(10000000, 100000000),
            "IsPlayerControlled": i == 0, # First country is player controlled
            "States": []
        }

        country_total_population = 0
        
        # Determine how many states to generate for this country
        # Use the number of states defined in the template, or default if not enough
        states_in_template = country_template.get("States", [])
        num_states_to_generate = min(num_states_per_country, len(states_in_template)) if states_in_template else num_states_per_country
        
        if not states_in_template and num_states_per_country > 0:
            # Fallback for countries with no predefined states: generate placeholder state names
            # This part can be improved if you want more structured random state generation for real countries without defined states
            states_to_iterate = [{"Name": f"State {chr(65+j)} of {country_name}", "Cities": [f"City {k+1}" for k in range(num_cities_per_state_min)]} for j in range(num_states_per_country)]
        elif not states_in_template:
             states_to_iterate = [] # No states if template is empty and num_states_per_country is 0
        else:
            # Cycle through available states in template if num_states_per_country is high
            states_to_iterate = [states_in_template[j % len(states_in_template)] for j in range(num_states_per_country)]


        for j_idx, state_template in enumerate(states_to_iterate):
            state_name = state_template["Name"]
            # Simple way to avoid duplicate state names (globally)
            s_instance_count = 1
            original_state_name = state_name
            while state_name in used_state_names_global_output:
                s_instance_count +=1
                state_name = f"{original_state_name} ({s_instance_count})" # Could also add country context
            used_state_names_global_output.add(state_name)
            
            state_data = {
                "Name": state_name,
                "TaxRate": round(random.uniform(0.04, country_data["TaxRate"] - 0.01 if country_data["TaxRate"] > 0.04 else 0.04 ), 2),
                "StateExpenses": random.randint(50000, country_data["NationalExpenses"] // (num_states_per_country + 2) if num_states_per_country > 0 else 50000),
                "InitialPopulation": 0,
                "InitialBudget": random.randint(500000, country_data["InitialBudget"] // (num_states_per_country + 2) if num_states_per_country > 0 else 500000),
                "Cities": []
            }
            state_total_population = 0
            
            # Determine how many cities to generate for this state
            cities_in_template = state_template.get("Cities", [])
            num_cities_to_generate = random.randint(num_cities_per_state_min, num_cities_per_state_min + 2) # Keep some randomness in city count
            
            if not cities_in_template and num_cities_to_generate > 0:
                cities_to_iterate = [f"City {k+1} in {state_name}" for k in range(num_cities_to_generate)]
            elif not cities_in_template:
                cities_to_iterate = []
            else:
                # Select a unique subset of template cities to avoid duplicates
                max_cities = min(len(cities_in_template), num_cities_to_generate)
                cities_to_iterate = random.sample(cities_in_template, max_cities)


            for k_idx, city_template_name in enumerate(cities_to_iterate):
                city_name = city_template_name
                # Simple way to avoid duplicate city names (globally)
                c_instance_count = 1
                original_city_name = city_name
                while city_name in used_city_names_global_output:
                    c_instance_count += 1
                    city_name = f"{original_city_name} ({c_instance_count})" # Could also add state/country context
                used_city_names_global_output.add(city_name)

                city_pop = random.randint(10000, 5000000)
                city_data = {
                    "Name": city_name,
                    "InitialPopulation": city_pop,
                    "InitialBudget": random.randint(50000, state_data["InitialBudget"] // (num_cities_to_generate+1) if num_cities_to_generate > 0 else 50000),
                    "TaxRate": round(random.uniform(0.02, state_data["TaxRate"] - 0.01 if state_data["TaxRate"] > 0.02 else 0.02), 2),
                    "CityExpenses": random.randint(
                        10000,
                        max(
                            state_data["StateExpenses"] // (num_cities_to_generate+1) if num_cities_to_generate > 0 else 10000,
                            10000
                        )
                    ),
                    "InitialFactories": []
                }
                state_total_population += city_pop

                num_factories = random.randint(1, 5)
                if available_factory_blueprints:
                    for _ in range(num_factories):
                        factory_type = random.choice(available_factory_blueprints)
                        city_data["InitialFactories"].append({
                            "FactoryTypeName": factory_type,
                            "Capacity": random.randint(1, 5)
                        })
                state_data["Cities"].append(city_data)

            state_data["InitialPopulation"] = state_total_population
            country_total_population += state_total_population
            country_data["States"].append(state_data)

        country_data["InitialPopulation"] = country_total_population
        world["Countries"].append(country_data)

    return json.dumps(world, indent=2)

if __name__ == "__main__":
    # Example: Generate data for 4 real countries, each with up to 5 of their defined states (or fewer if less are defined),
    # and each state with 3-5 of its defined cities (or fewer if less are defined).
    # If you want more than 4 countries, it will start repeating the defined countries.
    # If a country/state has fewer defined sub-regions than requested, it will use what's available.
    generated_json = generate_world_data(num_countries=15, num_states_per_country=5, num_cities_per_state_min=3)
    print(generated_json)