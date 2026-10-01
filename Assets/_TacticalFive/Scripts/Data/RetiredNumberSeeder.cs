using System.Collections.Generic;

/// <summary>
/// Dorsales retirados de leyendas históricas, por franquicia. Cada entrada lleva la
/// abreviatura del equipo explícita (no depende de que la leyenda esté en el Salón de la
/// Fama), de modo que el dorsal se siembra siempre en la franquicia correcta.
/// Se combina con <see cref="VeteranRetiredNumberSeeder"/> (veteranos activos).
/// </summary>
public static class RetiredNumberSeeder
{
    public static List<(string FirstName, string LastName, string Abbreviation, int Number)> Data => new()
    {
        // BOS
        ("Larry", "Bird", "BOS", 33),
        ("John", "Havlicek", "BOS", 17),
        ("Bob", "Cousy", "BOS", 14),
        ("Kevin", "McHale", "BOS", 32),
        ("Paul", "Pierce", "BOS", 34),
        // LAL
        ("Kareem", "Abdul-Jabbar", "LAL", 33),
        ("Kobe", "Bryant", "LAL", 24),
        ("Wilt", "Chamberlain", "LAL", 13),
        ("Jerry", "West", "LAL", 44),
        ("George", "Mikan", "LAL", 99),
        ("Shaquille", "O'Neal", "LAL", 34),
        ("Elgin", "Baylor", "LAL", 22),
        // CHI
        ("Michael", "Jordan", "CHI", 23),
        ("Scottie", "Pippen", "CHI", 33),
        ("Dennis", "Rodman", "CHI", 91),
        ("Artis", "Gilmore", "CHI", 53),
        // DET
        ("Isiah", "Thomas", "DET", 11),
        ("Joe", "Dumars", "DET", 4),
        ("Chauncey", "Billups", "DET", 1),
        ("Grant", "Hill", "DET", 32),
        ("Ben", "Wallace", "DET", 3),
        // PHI
        ("Julius", "Erving", "PHI", 6),
        ("Hal", "Greer", "PHI", 15),
        ("Billy", "Cunningham", "PHI", 32),
        ("Moses", "Malone", "PHI", 2),
        // NYK
        ("Patrick", "Ewing", "NYK", 33),
        ("Willis", "Reed", "NYK", 19),
        ("Walter", "Frazier", "NYK", 10),
        ("Earl", "Monroe", "NYK", 15),
        // PRT
        ("Clyde", "Drexler", "PRT", 22),
        ("Bill", "Walton", "PRT", 32),
        // MIA
        ("Dwyane", "Wade", "MIA", 3),
        ("Alonzo", "Mourning", "MIA", 33),
        // SAS
        ("David", "Robinson", "SAS", 50),
        ("Tony", "Parker", "SAS", 9),
        ("Manu", "Ginóbili", "SAS", 20),
        // HOU
        ("Hakeem", "Olajuwon", "HOU", 34),
        ("Yao", "Ming", "HOU", 11),
        // DAL
        ("Dirk", "Nowitzki", "DAL", 41),
        // IND
        ("Reggie", "Miller", "IND", 31),
        // MIN
        ("Kevin", "Garnett", "MIN", 21),
        // ORL
        ("Dwight", "Howard", "ORL", 12),
        ("Tracy", "McGrady", "ORL", 1),
        // BKN
        ("Jason", "Kidd", "BKN", 5),
        ("Dražen", "Petrović", "BKN", 3),
        // TOR
        ("Vince", "Carter", "TOR", 15),
        ("Chris", "Bosh", "TOR", 1),
        // SAC
        ("Oscar", "Robertson", "SAC", 14),
        ("Chris", "Webber", "SAC", 4),
        ("Vlade", "Divac", "SAC", 21),
        // UTA
        ("Karl", "Malone", "UTA", 32),
        ("Pete", "Maravich", "UTA", 7),
        // PHX
        ("Steve", "Nash", "PHX", 13),
        // GSW
        ("Rick", "Barry", "GSW", 24),
        // MEM
        ("Marc", "Gasol", "MEM", 33),
    };
}
