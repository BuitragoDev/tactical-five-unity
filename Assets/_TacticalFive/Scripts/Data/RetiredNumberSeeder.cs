using System.Collections.Generic;
using System.Linq;

public static class RetiredNumberSeeder
{
    public static List<(string FirstName, string LastName, int Number)> Data => new()
    {
        // BOS
        ("Larry", "Bird", 33),
        ("John", "Havlicek", 17),
        ("Bob", "Cousy", 14),
        ("Kevin", "McHale", 32),
        ("Paul", "Pierce", 34),
        // LAL
        ("Kareem", "Abdul-Jabbar", 33),
        ("Kobe", "Bryant", 24),
        ("Wilt", "Chamberlain", 13),
        ("Jerry", "West", 44),
        ("George", "Mikan", 99),
        ("Shaquille", "O'Neal", 34),
        ("Elgin", "Baylor", 22),
        // CHI
        ("Michael", "Jordan", 23),
        ("Scottie", "Pippen", 33),
        ("Dennis", "Rodman", 91),
        ("Artis", "Gilmore", 53),
        // DET
        ("Isiah", "Thomas", 11),
        ("Joe", "Dumars", 4),
        ("Chauncey", "Billups", 1),
        ("Grant", "Hill", 32),
        ("Ben", "Wallace", 3),
        // PHI
        ("Julius", "Erving", 6),
        ("Hal", "Greer", 15),
        ("Billy", "Cunningham", 32),
        ("Moses", "Malone", 2),
        // NYK
        ("Patrick", "Ewing", 33),
        ("Willis", "Reed", 19),
        ("Walter", "Frazier", 10),
        ("Earl", "Monroe", 15),
        // POR
        ("Clyde", "Drexler", 22),
        ("Bill", "Walton", 32),
        // MIA
        ("Dwyane", "Wade", 3),
        ("Alonzo", "Mourning", 33),
        // SAS
        ("David", "Robinson", 50),
        ("Tony", "Parker", 9),
        ("Manu", "Ginóbili", 20),
        // HOU
        ("Hakeem", "Olajuwon", 34),
        ("Yao", "Ming", 11),
        // DAL
        ("Dirk", "Nowitzki", 41),
        // IND
        ("Reggie", "Miller", 31),
        // MIN
        ("Kevin", "Garnett", 21),
        // ORL
        ("Dwight", "Howard", 12),
        ("Tracy", "McGrady", 1),
        // BKN
        ("Jason", "Kidd", 5),
        ("Dražen", "Petrović", 3),
        // TOR
        ("Vince", "Carter", 15),
        ("Chris", "Bosh", 1),
        // SAC
        ("Oscar", "Robertson", 14),
        ("Chris", "Webber", 4),
        ("Vlade", "Divac", 21),
        // UTA
        ("Karl", "Malone", 32),
        ("Pete", "Maravich", 7),
        // PHX
        ("Steve", "Nash", 13),
        // GSW
        ("Rick", "Barry", 24),
    };
}