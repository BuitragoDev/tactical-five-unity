using System.Collections.Generic;

/// <summary>
/// Generación de nombres coherentes con la nacionalidad. Los nombres se agrupan
/// por regiones culturales y cada país se mapea a una región, de modo que un
/// jugador lituano recibe nombres bálticos y uno español, nombres hispanos.
/// Fuente única para rookies, prospectos G-League y empleados.
///
/// Tamaños de pool: anglo = 100 nombres / 200 apellidos (por la alta proporción
/// de USA); el resto de regiones = 50 nombres / 50 apellidos.
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
                "Charles", "Thomas", "Steven", "Paul", "Kenneth", "George", "Timothy", "Edward",
                "Jeffrey", "Gary", "Nicholas", "Jonathan", "Stephen", "Larry", "Raymond",
                "Gregory", "Frank", "Patrick", "Jack", "Dennis", "Jerry", "Walter", "Keith",
                "Roger", "Terry", "Gerald", "Carl", "Harold", "Arthur", "Lawrence", "Jesse",
                "Bryan", "Billy", "Bruce", "Gabriel", "Albert", "Willie", "Alan", "Wayne",
                "Elijah", "Randy", "Vincent", "Owen", "Lucas",
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
                "Robertson", "Ellis", "Carter", "Phillips", "Parker", "Evans", "Turner",
                "Walsh", "Cunningham", "Snyder", "Armstrong", "Ferguson", "Hawkins", "Barrett",
                "Chapman", "Lambert", "Weaver", "Garrett", "Parks", "Watkins", "Payne",
                "Lawson", "McCarthy", "Dixon", "Boyd", "Bishop", "Fox", "Woods", "Newman",
                "Lynch", "Rice", "Potter", "Holland", "Kelley", "Chambers", "Grant", "Waters",
                "Cannon", "Barnett", "Hardy", "Sparks", "Riley", "Nichols", "Fowler", "Dean",
                "Gregory", "Norris", "Curtis", "Knight", "Lane", "Gilbert", "Franklin",
                "Pierce", "Robbins", "Wilkins", "Bartlett", "Ramsey", "Massey", "Buckley",
                "Pratt", "McDaniel", "Watts", "Hodges", "Blackwell", "Dorsey", "Cummings",
                "Whitaker", "Huff", "Cantrell", "Chaney", "Sherman", "Crosby", "Cameron",
                "Dodson", "Nash", "Pollard", "Whitfield", "McLeod", "Hobbs", "Hendrix",
                "Combs", "Kirby", "Bolton", "Holloway", "Barron", "Wilkinson", "McNeil",
                "Allison", "Faulkner", "Coffey", "Melton", "Strickland", "Kramer", "Wooten",
                "Burgess", "McKinney", "Dickson", "Rollins", "Swanson", "Grimes", "Roach",
                "Sweeney", "Duke", "Hines", "Rowe", "Dillon", "Chase", "Bonner", "Rush",
                "O'Donnell", "McCormick", "Preston", "Best", "Vaughan", "Gill", "Noble",
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
                "Eduardo", "Ricardo", "Roberto", "Francisco", "Antonio", "Daniel", "David",
                "Jorge", "Óscar", "Julio", "Ramón", "Salvador", "Vicente",
            },
            Last = new[]
            {
                "García", "Rodríguez", "Martínez", "Hernández", "López", "González", "Pérez",
                "Sánchez", "Ramírez", "Torres", "Flores", "Rivera", "Gómez", "Díaz", "Cruz",
                "Reyes", "Morales", "Gutiérrez", "Ortiz", "Ramos", "Chávez", "Mendoza",
                "Ruiz", "Álvarez", "Castillo", "Jiménez", "Vásquez", "Romero", "Moreno",
                "Herrera", "Medina", "Aguilar", "Vargas", "Rojas", "Silva", "Núñez", "Molina",
                "Delgado", "Castro", "Ortega", "Salazar", "Fuentes", "Cabrera", "Vega",
                "Peña", "Ríos", "Contreras", "Guerrero", "Campos", "Solís", "Buitrago",
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
                "Alexandre", "Antônio", "Arthur", "Bernardo", "Cauã", "Davi", "Enzo",
                "Fernando", "Guilherme", "Heitor", "Hugo", "Igor", "Joaquim", "Lorenzo",
                "Lucca", "Murilo", "Nicolas", "Otávio", "Samuel", "Vicente",
            },
            Last = new[]
            {
                "Silva", "Santos", "Oliveira", "Souza", "Lima", "Pereira", "Costa", "Ferreira",
                "Rodrigues", "Almeida", "Nascimento", "Carvalho", "Araújo", "Ribeiro", "Gomes",
                "Martins", "Barbosa", "Rocha", "Dias", "Monteiro", "Cardoso", "Teixeira",
                "Moreira", "Correia", "Fernandes", "Pinto", "Neves", "Mendes", "Machado",
                "Nunes", "Gonçalves", "Amaral", "Batista", "Cavalcanti", "Duarte", "Freitas",
                "Lopes", "Marques", "Melo", "Miranda", "Moraes", "Nogueira", "Peixoto",
                "Pires", "Ramos", "Reis", "Sampaio", "Tavares", "Vieira", "Cunha",
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
                "Adama", "Alphonse", "Arnaud", "Baba", "Charles", "David", "Étienne",
                "Fabrice", "Gaël", "Guillaume", "Idrissa", "Jacques", "Karim", "Lassana",
                "Malick", "Modou", "Olivier", "Pascal", "Régis", "Serge", "Vincent",
            },
            Last = new[]
            {
                "Diallo", "Traoré", "Camara", "Coulibaly", "Koné", "Diarra", "Cissé", "Touré",
                "Bah", "Barry", "Sow", "Ndiaye", "Diop", "Gueye", "Faye", "Sarr", "Dubois",
                "Moreau", "Lefebvre", "Laurent", "Simon", "Michel", "Leroy", "Roux", "Fournier",
                "Girard", "Bonnet", "Dupont", "Kouassi", "Bamba", "Keita", "Sylla", "Sangaré",
                "Doumbia", "Kanté", "Dembélé", "Sissoko", "Maïga", "Ouattara", "Thiam",
                "Petit", "Rousseau", "Blanc", "Henry", "Robin", "Marchand", "Noël", "Giraud",
                "Perrin", "Chevalier",
            },
        },

        ["african"] = new RegionPool
        {
            First = new[]
            {
                "Kwame", "Kofi", "Chidi", "Emeka", "Obinna", "Tunde", "Femi", "Ade", "Kunle",
                "Chinedu", "Nnamdi", "Kwesi", "Yaw", "Thabo", "Sipho", "Bongani", "Juma",
                "Baraka", "Amani", "Zola", "Tendai", "Sekou", "Musa", "Ike", "Chike",
                "Abiodun", "Ayo", "Bamidele", "Chima", "Efe", "Ekene", "Gbenga", "Ifeanyi",
                "Kalu", "Kayode", "Lwazi", "Mandla", "Ngozi", "Nkosinathi", "Olu",
                "Oluwaseun", "Segun", "Sizwe", "Tafadzwa", "Themba", "Uche", "Wale", "Yemi",
                "Zanele", "Sefu",
            },
            Last = new[]
            {
                "Okonkwo", "Mensah", "Okafor", "Adeyemi", "Osei", "Boateng", "Nwosu", "Eze",
                "Obi", "Achebe", "Mbeki", "Nkosi", "Dlamini", "Ndlovu", "Mokoena", "Kamau",
                "Otieno", "Wanjiru", "Abebe", "Bekele", "Owusu", "Adebayo", "Chukwu", "Mwangi",
                "Acheampong", "Adewale", "Amoah", "Asante", "Balogun", "Chirwa", "Danquah",
                "Frimpong", "Gyasi", "Kariuki", "Mabaso", "Mahlangu", "Mutombo", "Nkemelu",
                "Nkrumah", "Nwankwo", "Odhiambo", "Ofori", "Ogundele", "Okoro", "Olawale",
                "Onyeka", "Quaye", "Sibanda", "Tshabalala", "Zulu",
            },
        },

        ["arabic"] = new RegionPool
        {
            First = new[]
            {
                "Mohamed", "Ahmed", "Ali", "Omar", "Youssef", "Karim", "Samir", "Rachid",
                "Khalid", "Bilal", "Amir", "Hassan", "Hussein", "Tarek", "Nabil", "Yassine",
                "Mehdi", "Anas", "Ilyas", "Rayan", "Zakaria", "Hamza", "Abdul", "Adam",
                "Adil", "Amin", "Ayman", "Bassem", "Elias", "Faisal", "Farid", "Fouad",
                "Hicham", "Ibrahim", "Idris", "Ismael", "Jamal", "Kais", "Malek", "Marwan",
                "Mourad", "Mustafa", "Nizar", "Osman", "Rami", "Reda", "Said", "Salim",
                "Walid", "Yahya",
            },
            Last = new[]
            {
                "El Amrani", "Benali", "Haddad", "Khalil", "Nasser", "Mansour", "Rahmani",
                "Bouazizi", "Trabelsi", "Chaouki", "Zidane", "Ben Youssef", "Alaoui", "Cherif",
                "Farsi", "Karimi", "Rahimi", "Saleh", "Farah", "Aziz", "Ben Salah", "El Fassi",
                "Abbassi", "Amari", "Bakri", "Belkacem", "Ben Amor", "Bensalem", "Bouzid",
                "Cherkaoui", "Dridi", "El Idrissi", "El Masri", "Gharbi", "Hamdi", "Jaziri",
                "Kaddour", "Khoury", "Mahfoud", "Meziane", "Nasri", "Ouali", "Riad", "Saidi",
                "Slimani", "Toumi", "Youssef", "Zaidi", "Ziani", "Belaid",
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
                "Anton", "Bojan", "Branko", "Danilo", "Goran", "Jarosław", "Josip",
                "Konstantin", "Miroslav", "Nenad", "Ognjen", "Radoslav", "Sasa", "Stanislav",
                "Szymon", "Tomasz", "Uroš", "Vasilije", "Viktor", "Zoran",
            },
            Last = new[]
            {
                "Petrović", "Jovanović", "Nikolić", "Marković", "Popović", "Ilić", "Stojanović",
                "Kovač", "Horvat", "Novak", "Petrov", "Volkov", "Sokolov", "Ivanov", "Popov",
                "Kowalski", "Nowak", "Wójcik", "Kamiński", "Novák", "Svoboda", "Dvořák",
                "Shevchenko", "Kovalenko", "Bondarenko", "Đorđević", "Pavlović", "Milić",
                "Babić", "Blagojević", "Božić", "Cvetković", "Dujmović", "Filipović",
                "Kuznetsov", "Lukić", "Matić", "Morozov", "Nenadić", "Orlov", "Pavlenko",
                "Radovanović", "Savić", "Sokolović", "Todorović", "Vasiliev", "Vuković",
                "Zorić", "Zieliński", "Woźniak",
            },
        },

        ["baltic"] = new RegionPool
        {
            First = new[]
            {
                "Kristaps", "Dāvis", "Rihards", "Jānis", "Artūrs", "Rūdolfs", "Domantas",
                "Jonas", "Mantas", "Rokas", "Tadas", "Vytautas", "Marius", "Siim", "Karl",
                "Martin", "Rasmus", "Henri", "Kristjan", "Andris",
                "Aivar", "Arnas", "Augustas", "Dainius", "Edgars", "Eerik", "Giedrius",
                "Gustas", "Henrijs", "Indrek", "Jurgis", "Kārlis", "Kaspars", "Kęstutis",
                "Kristofer", "Laurynas", "Madis", "Mārtiņš", "Matas", "Mindaugas", "Paulius",
                "Priit", "Raivis", "Reinis", "Rimantas", "Saulius", "Tanel", "Tomas", "Urmas",
                "Vaidas",
            },
            Last = new[]
            {
                "Bērziņš", "Ozols", "Kalniņš", "Jankauskas", "Petrauskas", "Kazlauskas",
                "Vaitkus", "Žukauskas", "Sabonis", "Mažeika", "Tamm", "Saar", "Sepp", "Kask",
                "Mägi", "Liepiņš", "Balodis", "Stankevičius", "Adomaitis", "Balčiūnas",
                "Butkus", "Eglītis", "Grigas", "Ivanauskas", "Jankovskis", "Jokubaitis",
                "Kairys", "Kallas", "Kuusk", "Lapinskas", "Laurinavičius", "Lill", "Mets",
                "Männik", "Norkus", "Ozoliņš", "Pärn", "Puidet", "Raudsepp", "Reinhold",
                "Sarapuu", "Sild", "Strazdiņš", "Šimonis", "Teder", "Toom", "Urbonas", "Valk",
                "Vareikis", "Zvirgzdiņš",
            },
        },

        ["nordic"] = new RegionPool
        {
            First = new[]
            {
                "Erik", "Lars", "Anders", "Johan", "Magnus", "Nils", "Björn", "Sven", "Oskar",
                "Elias", "Emil", "Axel", "Gustav", "Viktor", "Henrik", "Mikael", "Juha",
                "Mikko", "Antti", "Ville", "Jussi", "Lauri", "Eero", "Sigurd", "Kasper",
                "Aleksander", "Andreas", "Arvid", "Birger", "Bo", "Dag", "Einar", "Filip",
                "Fredrik", "Håkon", "Harald", "Ivar", "Jens", "Joakim", "Knut", "Leif",
                "Mattias", "Morten", "Olav", "Olli", "Pekka", "Rasmus", "Sami", "Timo",
                "Øyvind",
            },
            Last = new[]
            {
                "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
                "Olsson", "Persson", "Svensson", "Gustafsson", "Virtanen", "Korhonen",
                "Mäkinen", "Nieminen", "Järvinen", "Hansen", "Johansen", "Larsen", "Nilsen",
                "Berg", "Lindqvist", "Holm", "Aalto", "Ahlberg", "Bengtsson", "Dahl",
                "Eriksen", "Fredriksson", "Haugen", "Heikkilä", "Håkansson", "Jakobsen",
                "Jokinen", "Koskinen", "Lahtinen", "Lindberg", "Lund", "Lundqvist", "Madsen",
                "Møller", "Nyström", "Rasmussen", "Salo", "Sandberg", "Solberg", "Sten",
                "Ström", "Tuominen", "Vik", "Ødegård",
            },
        },

        ["germanic"] = new RegionPool
        {
            First = new[]
            {
                "Hans", "Peter", "Klaus", "Stefan", "Thomas", "Michael", "Andreas", "Markus",
                "Lukas", "Felix", "Maximilian", "Jonas", "Leon", "Niklas", "Jan", "Lars",
                "Sven", "Tim", "Tom", "Daan", "Sem", "Bram", "Thijs", "Jeroen", "Pieter",
                "Willem", "Matthias", "Florian", "Sebastian", "Alexander", "Benedikt",
                "Christoph", "Daniel", "Fabian", "Hendrik", "Jannik", "Johannes", "Julian",
                "Kai", "Kilian", "Lennart", "Marco", "Nico", "Oliver", "Paul", "Ruben",
                "Simon", "Tobias", "Vincent", "Yannick",
            },
            Last = new[]
            {
                "Müller", "Schmidt", "Schneider", "Fischer", "Weber", "Meyer", "Wagner",
                "Becker", "Schulz", "Hoffmann", "Bauer", "Koch", "Richter", "Klein", "Wolf",
                "De Jong", "Van Dijk", "Visser", "Bakker", "Smit", "De Vries", "Van den Berg",
                "Jansen", "Vermeer", "Braun", "De Boer", "Van Leeuwen", "Van der Meer",
                "Krüger", "Lehmann", "Köhler", "Herrmann", "Walter", "König", "Mayer", "Huber",
                "Kaiser", "Fuchs", "Peters", "Lang", "Scholz", "Möller", "Weiß", "Jung",
                "Hahn", "Vogel", "Friedrich", "Keller", "Günther", "Frank",
            },
        },

        ["mediterranean"] = new RegionPool
        {
            First = new[]
            {
                "Marco", "Luca", "Matteo", "Alessandro", "Andrea", "Francesco", "Giovanni",
                "Lorenzo", "Giuseppe", "Antonio", "Giannis", "Kostas", "Nikos", "Dimitris",
                "Georgios", "Christos", "Emre", "Mehmet", "Burak", "Can", "Yusuf", "David",
                "Moshe", "Yossi", "Avi", "Paolo", "Salvatore", "Alessio", "Angelo",
                "Athanasios", "Carlo", "Dario", "Emanuele", "Enrico", "Fabio", "Gabriel",
                "Giorgio", "Hakan", "Ibrahim", "Konstantinos", "Leandro", "Leonidas", "Murat",
                "Panagiotis", "Roberto", "Selim", "Simone", "Stefano", "Theodoros", "Vassilis",
            },
            Last = new[]
            {
                "Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo",
                "Ricci", "Greco", "Conti", "Papadopoulos", "Nikolaidis", "Georgiou",
                "Dimitriou", "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Cohen", "Levi",
                "Mizrahi", "Peretz", "Marino", "Gallo", "Barone", "Battaglia", "Bruno",
                "Carbone", "De Luca", "Fontana", "Karaca", "Katsaros", "Lombardi", "Longo",
                "Mancini", "Moretti", "Özdemir", "Pagano", "Papadakis", "Pellegrini", "Rizzo",
                "Santoro", "Serra", "Sorrentino", "Theodorou", "Vitale", "Yıldız", "Aslan",
                "Arslan",
            },
        },

        ["east_asian"] = new RegionPool
        {
            First = new[]
            {
                "Yuki", "Haruto", "Sota", "Ren", "Kaito", "Riku", "Takumi", "Daiki", "Hiroto",
                "Kenji", "Wei", "Jun", "Hao", "Ming", "Lei", "Chen", "Ji-ho", "Min-jun",
                "Seo-jun", "Do-yun", "Ji-hoon", "Sho", "Yuto", "Aoi", "Dae-hyun", "Eun-woo",
                "Haruki", "Hyun-woo", "Itsuki", "Jin", "Kai", "Kazuki", "Keisuke", "Min-ho",
                "Ryo", "Ryusei", "Satoshi", "Seung-min", "Shin", "Taiga", "Tatsuya",
                "Tae-yang", "Xinyu", "Yamato", "Yejun", "Yi", "Yong", "Yuchen", "Zhen", "Zhi",
            },
            Last = new[]
            {
                "Tanaka", "Suzuki", "Takahashi", "Watanabe", "Ito", "Yamamoto", "Nakamura",
                "Kobayashi", "Kato", "Yoshida", "Kim", "Lee", "Park", "Choi", "Jung", "Kang",
                "Wang", "Li", "Zhang", "Liu", "Chen", "Yang", "Huang", "Ahn", "Bae", "Chang",
                "Cho", "Han", "Hayashi", "Ho", "Hong", "Im", "Ishikawa", "Jang", "Jeon",
                "Jiang", "Kwon", "Lim", "Lin", "Maeda", "Matsumoto", "Moon", "Mori", "Oh",
                "Okada", "Shin", "Shimizu", "Song", "Sun", "Wu",
            },
        },

        // Fallback para nacionalidades sin región asignada: mezcla internacional.
        [DefaultRegion] = new RegionPool
        {
            First = new[]
            {
                "Alex", "Sam", "Max", "Leo", "Adam", "Omar", "Karim", "Nikola", "Marco",
                "Diego", "Ivan", "Yuki", "Kwame", "Rafael", "Daniel", "David", "Kevin",
                "Bryan", "Joel", "Andrés", "Victor", "Hugo", "Aaron", "Amir", "Andre",
                "Carlos", "Elias", "Felix", "Gabriel", "Hassan", "Isaac", "Javier", "Jonas",
                "Lucas", "Malik", "Mateo", "Miguel", "Noah", "Oscar", "Pablo", "Rayan",
                "Samuel", "Santiago", "Tariq", "Theo", "Tomas", "Youssef", "Yusuf", "Zach",
                "Adrian",
            },
            Last = new[]
            {
                "Silva", "Rossi", "Kim", "Novak", "Garcia", "Müller", "Dubois", "Popov",
                "Haddad", "Tanaka", "Okafor", "Andersen", "Costa", "López", "Petrov",
                "Johnson", "Okonkwo", "Nasser", "Yamada", "Bauer", "Mensah", "Vargas",
                "Ahmed", "Ali", "Brown", "Chen", "Davis", "Diallo", "Fernandez", "Gonzalez",
                "Ivanov", "Jones", "Khan", "Lee", "Martinez", "Nakamura", "Nguyen", "Oliveira",
                "Park", "Pereira", "Rodriguez", "Sanchez", "Schmidt", "Singh", "Smith",
                "Suzuki", "Taylor", "Wang", "Yamaguchi", "Zhang",
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
