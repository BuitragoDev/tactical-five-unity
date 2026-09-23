using System.Collections.Generic;

/// <summary>
/// Generación de nombres coherentes con la nacionalidad. Los nombres se agrupan
/// por regiones culturales y cada país se mapea a una región, de modo que un
/// jugador lituano recibe nombres bálticos y uno español, nombres hispanos.
/// Fuente única para rookies, prospectos G-League y empleados.
/// </summary>
public static class NamePools
{
    public class RegionPool
    {
        public string[] First;
        public string[] Last;
    }

    public const string DefaultRegion = "international";

    // ── POOLS POR REGIÓN ──────────────────────────────────────────────

    static readonly Dictionary<string, RegionPool> Regions = new()
    {
        ["anglo"] = new RegionPool
        {
            First = new[]
            {
                "James", "John", "Michael", "William", "David", "Robert", "Joseph", "Daniel",
                "Matthew", "Andrew", "Joshua", "Christopher", "Anthony", "Ryan", "Tyler",
                "Brandon", "Justin", "Aaron", "Adam", "Nathan", "Ethan", "Noah", "Liam",
                "Mason", "Logan", "Jacob", "Evan", "Cameron", "Jordan", "Marcus", "Darius",
                "Jalen", "DeAndre", "Isaiah", "Xavier", "Terrance", "Devin", "Malik", "Andre",
                "Corey", "Trevor", "Cody", "Blake", "Chase", "Cole", "Jared", "Kyle", "Sean",
                "Brian", "Eric", "Kevin", "Jason", "Scott", "Dylan", "Austin", "Jeremy",
            },
            Last = new[]
            {
                "Smith", "Johnson", "Williams", "Brown", "Jones", "Miller", "Davis", "Wilson",
                "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Thompson",
                "White", "Harris", "Clark", "Lewis", "Robinson", "Walker", "Young", "Allen",
                "King", "Wright", "Scott", "Green", "Adams", "Nelson", "Baker", "Hall",
                "Campbell", "Mitchell", "Edwards", "Collins", "Stewart", "Morris", "Murphy",
                "Cook", "Rogers", "Morgan", "Cooper", "Peterson", "Bailey", "Reed", "Kelly",
                "Howard", "Cox", "Ward", "Richardson", "Watson", "Brooks", "Wood", "Bennett",
                "Gray", "Hughes", "Price", "Sanders", "Myers", "Long", "Ross", "Foster",
                "Powell", "Jenkins", "Perry", "Russell", "Sullivan", "Bell", "Coleman",
                "Butler", "Henderson", "Barnes", "Fisher", "Simmons", "Patterson", "Hamilton",
                "Graham", "Reynolds", "Griffin", "Wallace", "Harrison", "Marshall", "Webb",
            },
        },

        ["hispanic"] = new RegionPool
        {
            First = new[]
            {
                "José", "Juan", "Carlos", "Luis", "Miguel", "Ángel", "Javier", "Diego",
                "Alejandro", "Sergio", "Pablo", "Adrián", "Fernando", "Raúl", "Alberto",
                "Andrés", "Rubén", "Iván", "Manuel", "Pedro", "Rafael", "Víctor", "Hugo",
                "Mario", "Marco", "Nicolás", "Gabriel", "Santiago", "Mateo", "Sebastián",
                "Emilio", "Joaquín", "Tomás", "Gonzalo", "Rodrigo", "Esteban", "Cristian",
            },
            Last = new[]
            {
                "García", "Rodríguez", "Martínez", "Hernández", "López", "González", "Pérez",
                "Sánchez", "Ramírez", "Torres", "Flores", "Rivera", "Gómez", "Díaz", "Cruz",
                "Reyes", "Morales", "Gutiérrez", "Ortiz", "Ramos", "Chávez", "Mendoza",
                "Ruiz", "Álvarez", "Castillo", "Jiménez", "Vásquez", "Romero", "Moreno",
                "Herrera", "Medina", "Aguilar", "Vargas", "Rojas", "Silva", "Núñez", "Molina",
                "Delgado", "Castro", "Ortega", "Salazar", "Fuentes", "Cabrera", "Vega",
            },
        },

        ["portuguese"] = new RegionPool
        {
            First = new[]
            {
                "João", "Pedro", "Lucas", "Gabriel", "Rafael", "Thiago", "Matheus", "Bruno",
                "Felipe", "Gustavo", "Leonardo", "Vinícius", "Daniel", "Marcos", "Paulo",
                "Ricardo", "Eduardo", "André", "Tiago", "Diogo", "Rui", "Miguel", "Duarte",
                "Gonçalo", "Henrique", "Caio", "Douglas", "Fábio", "Renan", "Vítor",
            },
            Last = new[]
            {
                "Silva", "Santos", "Oliveira", "Souza", "Lima", "Pereira", "Costa", "Ferreira",
                "Rodrigues", "Almeida", "Nascimento", "Carvalho", "Araújo", "Ribeiro", "Gomes",
                "Martins", "Barbosa", "Rocha", "Dias", "Monteiro", "Cardoso", "Teixeira",
                "Moreira", "Correia", "Fernandes", "Pinto", "Neves", "Mendes", "Machado",
            },
        },

        ["francophone"] = new RegionPool
        {
            First = new[]
            {
                "Jean", "Pierre", "Louis", "Michel", "Antoine", "Thomas", "Nicolas", "Julien",
                "Mathieu", "Clément", "Théo", "Lucas", "Hugo", "Mamadou", "Ousmane",
                "Ibrahima", "Cheikh", "Moussa", "Abdoulaye", "Amadou", "Sékou", "Youssouf",
                "Emmanuel", "Cédric", "Franck", "Bakary", "Lamine", "Alioune", "Boubacar",
            },
            Last = new[]
            {
                "Diallo", "Traoré", "Camara", "Coulibaly", "Koné", "Diarra", "Cissé", "Touré",
                "Bah", "Barry", "Sow", "Ndiaye", "Diop", "Gueye", "Faye", "Sarr", "Dubois",
                "Moreau", "Lefebvre", "Laurent", "Simon", "Michel", "Leroy", "Roux", "Fournier",
                "Girard", "Bonnet", "Dupont", "Kouassi", "Bamba",
            },
        },

        ["african"] = new RegionPool
        {
            First = new[]
            {
                "Kwame", "Kofi", "Chidi", "Emeka", "Obinna", "Tunde", "Femi", "Ade", "Kunle",
                "Chinedu", "Nnamdi", "Kwesi", "Yaw", "Thabo", "Sipho", "Bongani", "Juma",
                "Baraka", "Amani", "Zola", "Tendai", "Sekou", "Musa", "Ike", "Chike",
            },
            Last = new[]
            {
                "Okonkwo", "Mensah", "Okafor", "Adeyemi", "Osei", "Boateng", "Nwosu", "Eze",
                "Obi", "Achebe", "Mbeki", "Nkosi", "Dlamini", "Ndlovu", "Mokoena", "Kamau",
                "Otieno", "Wanjiru", "Abebe", "Bekele", "Owusu", "Adebayo", "Chukwu", "Mwangi",
            },
        },

        ["arabic"] = new RegionPool
        {
            First = new[]
            {
                "Mohamed", "Ahmed", "Ali", "Omar", "Youssef", "Karim", "Samir", "Rachid",
                "Khalid", "Bilal", "Amir", "Hassan", "Hussein", "Tarek", "Nabil", "Yassine",
                "Mehdi", "Anas", "Ilyas", "Rayan", "Zakaria", "Hamza",
            },
            Last = new[]
            {
                "El Amrani", "Benali", "Haddad", "Khalil", "Nasser", "Mansour", "Rahmani",
                "Bouazizi", "Trabelsi", "Chaouki", "Zidane", "Ben Youssef", "Alaoui", "Cherif",
                "Farsi", "Karimi", "Rahimi", "Saleh", "Farah", "Aziz", "Ben Salah", "El Fassi",
            },
        },

        ["slavic"] = new RegionPool
        {
            First = new[]
            {
                "Nikola", "Luka", "Marko", "Stefan", "Miloš", "Ivan", "Petar", "Jovan",
                "Aleksandar", "Vladimir", "Dmitri", "Sergei", "Andrei", "Mikhail", "Pavel",
                "Bogdan", "Vlad", "Igor", "Tomáš", "Jakub", "Krzysztof", "Wojciech", "Mateusz",
                "Piotr", "Yuri", "Oleksandr", "Andriy", "Dušan", "Filip", "Lazar",
            },
            Last = new[]
            {
                "Petrović", "Jovanović", "Nikolić", "Marković", "Popović", "Ilić", "Stojanović",
                "Kovač", "Horvat", "Novak", "Petrov", "Volkov", "Sokolov", "Ivanov", "Popov",
                "Kowalski", "Nowak", "Wójcik", "Kamiński", "Novák", "Svoboda", "Dvořák",
                "Shevchenko", "Kovalenko", "Bondarenko", "Đorđević", "Pavlović", "Milić",
            },
        },

        ["baltic"] = new RegionPool
        {
            First = new[]
            {
                "Kristaps", "Dāvis", "Rihards", "Jānis", "Artūrs", "Rūdolfs", "Domantas",
                "Jonas", "Mantas", "Rokas", "Tadas", "Vytautas", "Marius", "Siim", "Karl",
                "Martin", "Rasmus", "Henri", "Kristjan", "Andris",
            },
            Last = new[]
            {
                "Bērziņš", "Ozols", "Kalniņš", "Jankauskas", "Petrauskas", "Kazlauskas",
                "Vaitkus", "Žukauskas", "Sabonis", "Mažeika", "Tamm", "Saar", "Sepp", "Kask",
                "Mägi", "Liepiņš", "Balodis", "Stankevičius",
            },
        },

        ["nordic"] = new RegionPool
        {
            First = new[]
            {
                "Erik", "Lars", "Anders", "Johan", "Magnus", "Nils", "Björn", "Sven", "Oskar",
                "Elias", "Emil", "Axel", "Gustav", "Viktor", "Henrik", "Mikael", "Juha",
                "Mikko", "Antti", "Ville", "Jussi", "Lauri", "Eero", "Sigurd", "Kasper",
            },
            Last = new[]
            {
                "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
                "Olsson", "Persson", "Svensson", "Gustafsson", "Virtanen", "Korhonen",
                "Mäkinen", "Nieminen", "Järvinen", "Hansen", "Johansen", "Larsen", "Nilsen",
                "Berg", "Lindqvist", "Holm",
            },
        },

        ["germanic"] = new RegionPool
        {
            First = new[]
            {
                "Hans", "Peter", "Klaus", "Stefan", "Thomas", "Michael", "Andreas", "Markus",
                "Lukas", "Felix", "Maximilian", "Jonas", "Leon", "Niklas", "Jan", "Lars",
                "Sven", "Tim", "Tom", "Daan", "Sem", "Bram", "Thijs", "Jeroen", "Pieter",
                "Willem", "Matthias", "Florian", "Sebastian",
            },
            Last = new[]
            {
                "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner",
                "Becker", "Schulz", "Hoffmann", "Bauer", "Koch", "Richter", "Klein", "Wolf",
                "De Jong", "Van Dijk", "Visser", "Bakker", "Smit", "De Vries", "Van den Berg",
                "Jansen", "Vermeer",
            },
        },

        ["mediterranean"] = new RegionPool
        {
            First = new[]
            {
                "Marco", "Luca", "Matteo", "Alessandro", "Andrea", "Francesco", "Giovanni",
                "Lorenzo", "Giuseppe", "Antonio", "Giannis", "Kostas", "Nikos", "Dimitris",
                "Georgios", "Christos", "Emre", "Mehmet", "Burak", "Can", "Yusuf", "David",
                "Moshe", "Yossi", "Avi", "Paolo", "Salvatore",
            },
            Last = new[]
            {
                "Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo",
                "Ricci", "Greco", "Conti", "Papadopoulos", "Nikolaidis", "Georgiou",
                "Dimitriou", "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Cohen", "Levi",
                "Mizrahi", "Peretz", "Marino", "Gallo",
            },
        },

        ["east_asian"] = new RegionPool
        {
            First = new[]
            {
                "Yuki", "Haruto", "Sota", "Ren", "Kaito", "Riku", "Takumi", "Daiki", "Hiroto",
                "Kenji", "Wei", "Jun", "Hao", "Ming", "Lei", "Chen", "Ji-ho", "Min-jun",
                "Seo-jun", "Do-yun", "Ji-hoon", "Sho", "Yuto",
            },
            Last = new[]
            {
                "Tanaka", "Suzuki", "Takahashi", "Watanabe", "Ito", "Yamamoto", "Nakamura",
                "Kobayashi", "Kato", "Yoshida", "Kim", "Lee", "Park", "Choi", "Jung", "Kang",
                "Wang", "Li", "Zhang", "Liu", "Chen", "Yang", "Huang",
            },
        },

        // Fallback para nacionalidades sin región asignada: mezcla internacional.
        [DefaultRegion] = new RegionPool
        {
            First = new[]
            {
                "Alex", "Sam", "Max", "Leo", "Adam", "Omar", "Karim", "Nikola", "Marco",
                "Diego", "Ivan", "Yuki", "Kwame", "Rafael", "Daniel", "David", "Kevin",
                "Bryan", "Joel", "Andrés", "Victor", "Hugo",
            },
            Last = new[]
            {
                "Silva", "Rossi", "Kim", "Novak", "Garcia", "Müller", "Dubois", "Popov",
                "Haddad", "Tanaka", "Okafor", "Andersen", "Costa", "López", "Petrov",
                "Johnson", "Okonkwo", "Nasser", "Yamada", "Bauer", "Mensah", "Vargas",
            },
        },
    };

    // ── PAÍS → REGIÓN ─────────────────────────────────────────────────

    static readonly Dictionary<string, string> CountryToRegion = new()
    {
        // anglo
        ["USA"] = "anglo", ["GBR"] = "anglo", ["CAN"] = "anglo", ["AUS"] = "anglo",
        ["NZL"] = "anglo", ["JAM"] = "anglo", ["LCA"] = "anglo", ["BHS"] = "anglo",
        ["IRL"] = "anglo", ["BAH"] = "anglo",
        // hispanic
        ["ESP"] = "hispanic", ["DOM"] = "hispanic", ["ARG"] = "hispanic", ["MEX"] = "hispanic",
        ["PRI"] = "hispanic", ["VEN"] = "hispanic", ["COL"] = "hispanic", ["URY"] = "hispanic",
        ["PAN"] = "hispanic", ["CUB"] = "hispanic", ["CHL"] = "hispanic", ["PER"] = "hispanic",
        ["ECU"] = "hispanic", ["GTM"] = "hispanic", ["HND"] = "hispanic", ["NIC"] = "hispanic",
        ["CRI"] = "hispanic", ["SLV"] = "hispanic", ["BOL"] = "hispanic", ["PRY"] = "hispanic",
        ["PHL"] = "hispanic",
        // portuguese
        ["BRA"] = "portuguese", ["PRT"] = "portuguese", ["POR"] = "portuguese",
        ["ANG"] = "portuguese", ["GNB"] = "portuguese", ["CPV"] = "portuguese",
        ["MOZ"] = "portuguese",
        // francophone
        ["FRA"] = "francophone", ["SEN"] = "francophone", ["CMR"] = "francophone",
        ["CIV"] = "francophone", ["MLI"] = "francophone", ["GUI"] = "francophone",
        ["BEN"] = "francophone", ["TGO"] = "francophone", ["COD"] = "francophone",
        ["GAB"] = "francophone", ["MTQ"] = "francophone", ["HTI"] = "francophone",
        ["BFA"] = "francophone", ["NER"] = "francophone", ["TCD"] = "francophone",
        ["CAF"] = "francophone", ["COG"] = "francophone", ["DJI"] = "francophone",
        // african (anglophone)
        ["NGA"] = "african", ["GHA"] = "african", ["GMB"] = "african", ["SSD"] = "african",
        ["UGA"] = "african", ["KEN"] = "african", ["ZAF"] = "african", ["TZA"] = "african",
        ["ZWE"] = "african",
        // arabic
        ["MAR"] = "arabic", ["DZA"] = "arabic", ["TUN"] = "arabic", ["EGY"] = "arabic",
        ["LBY"] = "arabic", ["LBN"] = "arabic", ["SYR"] = "arabic", ["IRQ"] = "arabic",
        ["JOR"] = "arabic", ["SAU"] = "arabic", ["ARE"] = "arabic", ["QAT"] = "arabic",
        ["KWT"] = "arabic", ["BHR"] = "arabic", ["OMN"] = "arabic", ["YEM"] = "arabic",
        // slavic
        ["SRB"] = "slavic", ["HRV"] = "slavic", ["BIH"] = "slavic", ["MNE"] = "slavic",
        ["SVN"] = "slavic", ["SLO"] = "slavic", ["MKD"] = "slavic", ["BGR"] = "slavic",
        ["POL"] = "slavic", ["CZE"] = "slavic", ["SVK"] = "slavic", ["RUS"] = "slavic",
        ["UKR"] = "slavic", ["BLR"] = "slavic",
        // baltic
        ["LTU"] = "baltic", ["LVA"] = "baltic", ["LAT"] = "baltic", ["EST"] = "baltic",
        // nordic
        ["SWE"] = "nordic", ["FIN"] = "nordic", ["NOR"] = "nordic", ["DNK"] = "nordic",
        ["ISL"] = "nordic",
        // germanic
        ["DEU"] = "germanic", ["AUT"] = "germanic", ["SUI"] = "germanic", ["NED"] = "germanic",
        ["BEL"] = "germanic", ["LUX"] = "germanic",
        // mediterranean
        ["ITA"] = "mediterranean", ["GRC"] = "mediterranean", ["GRE"] = "mediterranean",
        ["TUR"] = "mediterranean", ["ISR"] = "mediterranean", ["MLT"] = "mediterranean",
        ["CYP"] = "mediterranean",
        // east asian
        ["CHN"] = "east_asian", ["JPN"] = "east_asian", ["KOR"] = "east_asian",
        ["TWN"] = "east_asian", ["VNM"] = "east_asian",
    };

    // ── NACIONALIDADES DE JUGADOR GENERADO ────────────────────────────
    // Códigos válidos según CountryCodes (con bandera). 90% USA + resto.
    static readonly string[] NationalityPool =
    {
        "GER", "BRA", "GMB", "TGO", "AUS", "PNG", "AUT", "BHS", "NGA", "BEL",
        "MLI", "BIH", "CMR", "CAN", "HTI", "CHN", "HRV", "SVN", "ESP", "FIN",
        "FRA", "BEN", "CIV", "GUI", "MAR", "COD", "SEN", "GEO", "GRC", "ISR",
        "ITA", "JAM", "JPN", "LVA", "LTU", "MNE", "TUR", "NZL", "NED", "PRT",
        "GNB", "GBR", "POL", "CZE", "DOM", "RUS", "LCA", "SRB", "SWE", "SSD",
        "UGA", "SUI", "ANG", "UKR",
    };

    // ── API ───────────────────────────────────────────────────────────

    public static string RegionFor(string nationality)
    {
        if (string.IsNullOrEmpty(nationality)) return DefaultRegion;
        return CountryToRegion.TryGetValue(nationality.ToUpper(), out var region)
            ? region
            : DefaultRegion;
    }

    static RegionPool PoolFor(string nationality)
    {
        return Regions.TryGetValue(RegionFor(nationality), out var pool)
            ? pool
            : Regions[DefaultRegion];
    }

    /// <summary>Genera nombre y apellido coherentes con la nacionalidad (main thread).</summary>
    public static (string first, string last) Generate(string nationality)
    {
        var pool = PoolFor(nationality);
        return (pool.First[UnityEngine.Random.Range(0, pool.First.Length)],
                pool.Last[UnityEngine.Random.Range(0, pool.Last.Length)]);
    }

    /// <summary>Genera nombre y apellido coherentes con la nacionalidad (hilos de fondo).</summary>
    public static (string first, string last) Generate(string nationality, System.Random rng)
    {
        var pool = PoolFor(nationality);
        return (pool.First[rng.Next(pool.First.Length)],
                pool.Last[rng.Next(pool.Last.Length)]);
    }

    /// <summary>Nacionalidad de jugador generado: 90% USA, resto repartido.</summary>
    public static string RandomPlayerNationality()
    {
        return UnityEngine.Random.value < 0.9f
            ? "USA"
            : NationalityPool[UnityEngine.Random.Range(0, NationalityPool.Length)];
    }

    public static string RandomPlayerNationality(System.Random rng)
    {
        return rng.NextDouble() < 0.9
            ? "USA"
            : NationalityPool[rng.Next(NationalityPool.Length)];
    }

    /// <summary>Nombres para contextos sin nacionalidad (p. ej. prospectos G-League).</summary>
    public static (string first, string last) GeneratePlayerNames(System.Random rng)
    {
        return Generate(RandomPlayerNationality(rng), rng);
    }
}
