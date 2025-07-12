using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    // Generates initial world data similar to generate_world.py
    public static class WorldDataGenerator
    {
        private class StateTemplate
        {
            public string Name { get; set; }
            public List<string> Cities { get; set; }
        }

        private class CountryTemplate
        {
            public string Name { get; set; }
            public List<StateTemplate> States { get; set; }
        }

        private static readonly List<CountryTemplate> realWorldTemplate =
new List<CountryTemplate>
{
    new CountryTemplate
    {
        Name = "United States of America",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "New York",
                Cities = new List<string>
                {
                    "New York City",
                    "Buffalo",
                    "Albany",
                }
            },
            new StateTemplate
            {
                Name = "California",
                Cities = new List<string>
                {
                    "Los Angeles",
                    "San Francisco",
                    "San Diego",
                }
            },
            new StateTemplate
            {
                Name = "Texas",
                Cities = new List<string>
                {
                    "Houston",
                    "Dallas",
                    "Austin",
                }
            },
            new StateTemplate
            {
                Name = "Illinois",
                Cities = new List<string>
                {
                    "Chicago",
                    "Springfield",
                    "Peoria",
                }
            },
            new StateTemplate
            {
                Name = "Pennsylvania",
                Cities = new List<string>
                {
                    "Philadelphia",
                    "Pittsburgh",
                    "Harrisburg",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "German Empire",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Prussia",
                Cities = new List<string>
                {
                    "Berlin",
                    "Konigsberg",
                    "Danzig",
                }
            },
            new StateTemplate
            {
                Name = "Bavaria",
                Cities = new List<string>
                {
                    "Munich",
                    "Nuremberg",
                    "Augsburg",
                }
            },
            new StateTemplate
            {
                Name = "Saxony",
                Cities = new List<string>
                {
                    "Dresden",
                    "Leipzig",
                    "Chemnitz",
                }
            },
            new StateTemplate
            {
                Name = "Württemberg",
                Cities = new List<string>
                {
                    "Stuttgart",
                    "Ulm",
                    "Heilbronn",
                }
            },
            new StateTemplate
            {
                Name = "Alsace-Lorraine",
                Cities = new List<string>
                {
                    "Strasbourg",
                    "Metz",
                    "Mulhouse",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Empire of Japan",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Kanto",
                Cities = new List<string>
                {
                    "Tokyo",
                    "Yokohama",
                    "Chiba",
                }
            },
            new StateTemplate
            {
                Name = "Kansai",
                Cities = new List<string>
                {
                    "Osaka",
                    "Kyoto",
                    "Kobe",
                }
            },
            new StateTemplate
            {
                Name = "Kyushu",
                Cities = new List<string>
                {
                    "Fukuoka",
                    "Nagasaki",
                    "Kumamoto",
                }
            },
            new StateTemplate
            {
                Name = "Hokkaido",
                Cities = new List<string>
                {
                    "Sapporo",
                    "Hakodate",
                    "Asahikawa",
                }
            },
            new StateTemplate
            {
                Name = "Chugoku",
                Cities = new List<string>
                {
                    "Hiroshima",
                    "Okayama",
                    "Yamaguchi",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Republic of Brazil",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Rio de Janeiro State",
                Cities = new List<string>
                {
                    "Rio de Janeiro City",
                    "Niterói",
                    "Petrópolis",
                }
            },
            new StateTemplate
            {
                Name = "São Paulo State",
                Cities = new List<string>
                {
                    "São Paulo City",
                    "Campinas",
                    "Santos",
                }
            },
            new StateTemplate
            {
                Name = "Minas Gerais",
                Cities = new List<string>
                {
                    "Belo Horizonte",
                    "Ouro Preto",
                    "Juiz de Fora",
                }
            },
            new StateTemplate
            {
                Name = "Bahia",
                Cities = new List<string>
                {
                    "Salvador",
                    "Feira de Santana",
                    "Vitória da Conquista",
                }
            },
            new StateTemplate
            {
                Name = "Pernambuco",
                Cities = new List<string>
                {
                    "Recife",
                    "Olinda",
                    "Caruaru",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "United Kingdom",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "England",
                Cities = new List<string>
                {
                    "London",
                    "Manchester",
                    "Birmingham",
                }
            },
            new StateTemplate
            {
                Name = "Scotland",
                Cities = new List<string>
                {
                    "Edinburgh",
                    "Glasgow",
                    "Aberdeen",
                }
            },
            new StateTemplate
            {
                Name = "Wales",
                Cities = new List<string>
                {
                    "Cardiff",
                    "Swansea",
                    "Newport",
                }
            },
            new StateTemplate
            {
                Name = "Ireland (Whole Island)",
                Cities = new List<string>
                {
                    "Dublin",
                    "Belfast",
                    "Cork",
                }
            },
            new StateTemplate
            {
                Name = "Northumbria",
                Cities = new List<string>
                {
                    "Newcastle",
                    "Sunderland",
                    "Durham",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "French Republic",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Île-de-France",
                Cities = new List<string>
                {
                    "Paris",
                    "Versailles",
                    "Saint-Denis",
                }
            },
            new StateTemplate
            {
                Name = "Provence-Alpes-Côte d'Azur",
                Cities = new List<string>
                {
                    "Marseille",
                    "Nice",
                    "Toulon",
                }
            },
            new StateTemplate
            {
                Name = "Occitanie",
                Cities = new List<string>
                {
                    "Toulouse",
                    "Montpellier",
                    "Nîmes",
                }
            },
            new StateTemplate
            {
                Name = "Auvergne-Rhône-Alpes",
                Cities = new List<string>
                {
                    "Lyon",
                    "Grenoble",
                    "Saint-Étienne",
                }
            },
            new StateTemplate
            {
                Name = "Brittany",
                Cities = new List<string>
                {
                    "Rennes",
                    "Brest",
                    "Nantes",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Russian Empire",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Moscow Governorate",
                Cities = new List<string>
                {
                    "Moscow",
                    "Tver",
                    "Ryazan",
                }
            },
            new StateTemplate
            {
                Name = "Saint Petersburg Governorate",
                Cities = new List<string>
                {
                    "Saint Petersburg",
                    "Novgorod",
                    "Pskov",
                }
            },
            new StateTemplate
            {
                Name = "Kiev Governorate",
                Cities = new List<string>
                {
                    "Kiev",
                    "Chernigov",
                    "Poltava",
                }
            },
            new StateTemplate
            {
                Name = "Siberia General Governorate",
                Cities = new List<string>
                {
                    "Irkutsk",
                    "Omsk",
                    "Tomsk",
                }
            },
            new StateTemplate
            {
                Name = "Caucasus Viceroyalty",
                Cities = new List<string>
                {
                    "Tiflis (Tbilisi)",
                    "Baku",
                    "Yerevan",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Austro-Hungarian Empire",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Austria Proper",
                Cities = new List<string>
                {
                    "Vienna",
                    "Linz",
                    "Salzburg",
                }
            },
            new StateTemplate
            {
                Name = "Kingdom of Hungary",
                Cities = new List<string>
                {
                    "Budapest",
                    "Debrecen",
                    "Szeged",
                }
            },
            new StateTemplate
            {
                Name = "Kingdom of Bohemia",
                Cities = new List<string>
                {
                    "Prague",
                    "Brno",
                    "Pilsen",
                }
            },
            new StateTemplate
            {
                Name = "Galicia and Lodomeria",
                Cities = new List<string>
                {
                    "Lviv (Lemberg)",
                    "Krakow",
                    "Ternopil",
                }
            },
            new StateTemplate
            {
                Name = "Croatia-Slavonia",
                Cities = new List<string>
                {
                    "Zagreb (Agram)",
                    "Osijek",
                    "Rijeka (Fiume)",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Ottoman Empire",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Constantinople Vilayet",
                Cities = new List<string>
                {
                    "Constantinople (Istanbul)",
                    "Adrianople (Edirne)",
                    "Gallipoli",
                }
            },
            new StateTemplate
            {
                Name = "Anatolia Vilayet",
                Cities = new List<string>
                {
                    "Smyrna (Izmir)",
                    "Ankara",
                    "Konya",
                }
            },
            new StateTemplate
            {
                Name = "Syria Vilayet",
                Cities = new List<string>
                {
                    "Damascus",
                    "Beirut",
                    "Aleppo",
                }
            },
            new StateTemplate
            {
                Name = "Egypt Khedivate",
                Cities = new List<string>
                {
                    "Cairo",
                    "Alexandria",
                    "Port Said",
                }
            },
            new StateTemplate
            {
                Name = "Rumelia Eyalet",
                Cities = new List<string>
                {
                    "Thessaloniki (Salonica)",
                    "Monastir (Bitola)",
                    "Skopje (Üsküp)",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Kingdom of Italy",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Piedmont",
                Cities = new List<string>
                {
                    "Turin",
                    "Alessandria",
                    "Novara",
                }
            },
            new StateTemplate
            {
                Name = "Lombardy",
                Cities = new List<string>
                {
                    "Milan",
                    "Brescia",
                    "Bergamo",
                }
            },
            new StateTemplate
            {
                Name = "Kingdom of Naples (Two Sicilies)",
                Cities = new List<string>
                {
                    "Naples",
                    "Palermo",
                    "Bari",
                }
            },
            new StateTemplate
            {
                Name = "Tuscany",
                Cities = new List<string>
                {
                    "Florence",
                    "Pisa",
                    "Siena",
                }
            },
            new StateTemplate
            {
                Name = "Venetia",
                Cities = new List<string>
                {
                    "Venice",
                    "Verona",
                    "Padua",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Qing China",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Zhili Province",
                Cities = new List<string>
                {
                    "Peking (Beijing)",
                    "Tianjin",
                    "Baoding",
                }
            },
            new StateTemplate
            {
                Name = "Jiangsu Province",
                Cities = new List<string>
                {
                    "Nanking (Nanjing)",
                    "Shanghai",
                    "Suzhou",
                }
            },
            new StateTemplate
            {
                Name = "Guangdong Province",
                Cities = new List<string>
                {
                    "Canton (Guangzhou)",
                    "Shenzhen",
                    "Foshan",
                }
            },
            new StateTemplate
            {
                Name = "Sichuan Province",
                Cities = new List<string>
                {
                    "Chengdu",
                    "Chongqing",
                    "Zigong",
                }
            },
            new StateTemplate
            {
                Name = "Shandong Province",
                Cities = new List<string>
                {
                    "Jinan",
                    "Qingdao",
                    "Yantai",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Kingdom of Spain",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "New Castile",
                Cities = new List<string>
                {
                    "Madrid",
                    "Toledo",
                    "Guadalajara",
                }
            },
            new StateTemplate
            {
                Name = "Old Castile and León",
                Cities = new List<string>
                {
                    "Valladolid",
                    "Burgos",
                    "Salamanca",
                }
            },
            new StateTemplate
            {
                Name = "Catalonia",
                Cities = new List<string>
                {
                    "Barcelona",
                    "Tarragona",
                    "Girona",
                }
            },
            new StateTemplate
            {
                Name = "Andalusia",
                Cities = new List<string>
                {
                    "Seville",
                    "Málaga",
                    "Granada",
                }
            },
            new StateTemplate
            {
                Name = "Aragon",
                Cities = new List<string>
                {
                    "Zaragoza",
                    "Huesca",
                    "Teruel",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Dominion of Canada",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Ontario",
                Cities = new List<string>
                {
                    "Toronto",
                    "Ottawa",
                    "Hamilton",
                }
            },
            new StateTemplate
            {
                Name = "Quebec",
                Cities = new List<string>
                {
                    "Montreal",
                    "Quebec City",
                    "Sherbrooke",
                }
            },
            new StateTemplate
            {
                Name = "Nova Scotia",
                Cities = new List<string>
                {
                    "Halifax",
                    "Sydney",
                    "Dartmouth",
                }
            },
            new StateTemplate
            {
                Name = "British Columbia",
                Cities = new List<string>
                {
                    "Victoria",
                    "Vancouver",
                    "New Westminster",
                }
            },
            new StateTemplate
            {
                Name = "Manitoba",
                Cities = new List<string>
                {
                    "Winnipeg",
                    "Brandon",
                    "Portage la Prairie",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "Argentine Republic",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Buenos Aires Province",
                Cities = new List<string>
                {
                    "Buenos Aires City",
                    "La Plata",
                    "Mar del Plata",
                }
            },
            new StateTemplate
            {
                Name = "Córdoba Province",
                Cities = new List<string>
                {
                    "Córdoba City",
                    "Río Cuarto",
                    "Villa María",
                }
            },
            new StateTemplate
            {
                Name = "Santa Fe Province",
                Cities = new List<string>
                {
                    "Rosario",
                    "Santa Fe City",
                    "Rafaela",
                }
            },
            new StateTemplate
            {
                Name = "Mendoza Province",
                Cities = new List<string>
                {
                    "Mendoza City",
                    "San Rafael",
                    "Godoy Cruz",
                }
            },
            new StateTemplate
            {
                Name = "Tucumán Province",
                Cities = new List<string>
                {
                    "San Miguel de Tucumán",
                    "Yerba Buena",
                    "Tafí Viejo",
                }
            },
        }
    },
    new CountryTemplate
    {
        Name = "United Mexican States",
        States = new List<StateTemplate>
        {
            new StateTemplate
            {
                Name = "Federal District",
                Cities = new List<string>
                {
                    "Mexico City",
                    "Xochimilco",
                    "Tlalpan",
                }
            },
            new StateTemplate
            {
                Name = "Jalisco",
                Cities = new List<string>
                {
                    "Guadalajara",
                    "Zapopan",
                    "Tlaquepaque",
                }
            },
            new StateTemplate
            {
                Name = "Nuevo León",
                Cities = new List<string>
                {
                    "Monterrey",
                    "Guadalupe",
                    "Apodaca",
                }
            },
            new StateTemplate
            {
                Name = "Veracruz",
                Cities = new List<string>
                {
                    "Veracruz City",
                    "Xalapa",
                    "Coatzacoalcos",
                }
            },
            new StateTemplate
            {
                Name = "Puebla",
                Cities = new List<string>
                {
                    "Puebla City",
                    "Tehuacán",
                    "Cholula",
                }
            },
        }
    },
};

        private static readonly string[] availableFactoryBlueprints =
        {
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
        };

        private static double RandomBetween(Random rnd, double min, double max)
        {
            return min + rnd.NextDouble() * (max - min);
        }

        public static WorldSetupData GenerateWorldData(int numCountries = 10, int numStatesPerCountry = 5, int numCitiesPerStateMin = 3)
        {
            var rnd = new Random();
            var world = new WorldSetupData { Countries = new List<CountryData>(), ConstructionCompanies = new List<ConstructionCompanyData>() };
            var usedCountryNames = new HashSet<string>();
            var usedStateNames = new HashSet<string>();
            var usedCityNames = new HashSet<string>();

            for (int i = 0; i < numCountries; i++)
            {
                var countryTemplate = realWorldTemplate[i % realWorldTemplate.Count];
                string countryName = countryTemplate.Name;
                int instance = 1;
                string originalCountryName = countryName;
                while (usedCountryNames.Contains(countryName))
                {
                    instance++;
                    countryName = $"{originalCountryName} ({instance})";
                }
                usedCountryNames.Add(countryName);

                var countryData = new CountryData
                {
                    Name = countryName,
                    TaxRate = Math.Round(RandomBetween(rnd, 0.05, 0.25), 2),
                    NationalExpenses = rnd.Next(500000, 20000001),
                    InitialPopulation = 0,
                    InitialBudget = rnd.Next(10000000, 100000001),
                    IsPlayerControlled = i == 0,
                    States = new List<StateData>()
                };

                int countryTotalPopulation = 0;
                var statesInTemplate = countryTemplate.States ?? new List<StateTemplate>();
                int numStatesToGenerate = statesInTemplate.Count > 0 ? Math.Min(numStatesPerCountry, statesInTemplate.Count) : numStatesPerCountry;
                List<StateTemplate> statesToIterate;
                if (statesInTemplate.Count == 0)
                {
                    statesToIterate = Enumerable.Range(0, numStatesPerCountry)
                        .Select(j => new StateTemplate
                        {
                            Name = $"State {(char)('A' + j)} of {countryName}",
                            Cities = Enumerable.Range(1, numCitiesPerStateMin).Select(k => $"City {k}").ToList()
                        }).ToList();
                }
                else
                {
                    statesToIterate = Enumerable.Range(0, numStatesToGenerate)
                        .Select(j => statesInTemplate[j % statesInTemplate.Count]).ToList();
                }

                foreach (var stateTemplate in statesToIterate)
                {
                    string stateName = stateTemplate.Name;
                    int sInstance = 1;
                    string origStateName = stateName;
                    while (usedStateNames.Contains(stateName))
                    {
                        sInstance++;
                        stateName = $"{origStateName} ({sInstance})";
                    }
                    usedStateNames.Add(stateName);

                    var stateData = new StateData
                    {
                        Name = stateName,
                        TaxRate = Math.Round(RandomBetween(rnd, 0.04, Math.Max(countryData.TaxRate - 0.01, 0.04)), 2),
                        StateExpenses = rnd.Next(50000, numStatesPerCountry > 0 ? (int)(countryData.NationalExpenses / (numStatesPerCountry + 2)) : 50001),
                        InitialPopulation = 0,
                        InitialBudget = rnd.Next(500000, numStatesPerCountry > 0 ? (int)(countryData.InitialBudget / (numStatesPerCountry + 2)) : 500001),
                        Cities = new List<CityData>()
                    };

                    int stateTotalPopulation = 0;
                    var citiesInTemplate = stateTemplate.Cities ?? new List<string>();
                    int numCitiesToGenerate = rnd.Next(numCitiesPerStateMin, numCitiesPerStateMin + 3);
                    List<string> citiesToIterate;
                    if (citiesInTemplate.Count == 0)
                    {
                        citiesToIterate = Enumerable.Range(0, numCitiesToGenerate)
                            .Select(k => $"City {k + 1} in {stateName}").ToList();
                    }
                    else
                    {
                        int maxCities = Math.Min(citiesInTemplate.Count, numCitiesToGenerate);
                        citiesToIterate = citiesInTemplate.OrderBy(_ => rnd.Next()).Take(maxCities).ToList();
                    }

                    foreach (var cityTemplate in citiesToIterate)
                    {
                        string cityName = cityTemplate;
                        int cInstance = 1;
                        string origCityName = cityName;
                        while (usedCityNames.Contains(cityName))
                        {
                            cInstance++;
                            cityName = $"{origCityName} ({cInstance})";
                        }
                        usedCityNames.Add(cityName);

                        int cityPop = rnd.Next(10000, 5000001);
                        var cityData = new CityData
                        {
                            Name = cityName,
                            InitialPopulation = cityPop,
                            InitialBudget = rnd.Next(50000, numCitiesToGenerate > 0 ? (int)(stateData.InitialBudget / (numCitiesToGenerate + 1)) : 50001),
                            TaxRate = Math.Round(RandomBetween(rnd, 0.02, Math.Max(stateData.TaxRate - 0.01, 0.02)), 2),
                            CityExpenses = rnd.Next(10000, (int)Math.Max(stateData.StateExpenses / (numCitiesToGenerate + 1), 10000) + 1),
                            InitialFactories = new List<InitialFactoryData>()
                        };

                        world.ConstructionCompanies.Add(new ConstructionCompanyData
                        {
                            Name = $"{cityName} Builders",
                            HomeCity = cityName,
                            InitialBudget = rnd.Next(200000, 1000001),
                            Workers = rnd.Next(50, 201)
                        });

                        stateTotalPopulation += cityPop;
                        int numFactories = rnd.Next(1, 6);
                        for (int f = 0; f < numFactories; f++)
                        {
                            string factoryType = availableFactoryBlueprints[rnd.Next(availableFactoryBlueprints.Length)];
                            cityData.InitialFactories.Add(new InitialFactoryData
                            {
                                FactoryTypeName = factoryType,
                                Capacity = rnd.Next(1, 6)
                            });
                        }

                        stateData.Cities.Add(cityData);
                    }

                    stateData.InitialPopulation = stateTotalPopulation;
                    countryTotalPopulation += stateTotalPopulation;
                    countryData.States.Add(stateData);
                }

                countryData.InitialPopulation = countryTotalPopulation;
                world.Countries.Add(countryData);
            }

            return world;
        }
    }
}
