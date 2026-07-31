using UnityEngine;
using System.IO;
using System.Collections.Generic;
using SQLite;
using System;
using System.Linq;
using System.Globalization;

public partial class DatabaseManager
{
    // ── MANAGER ────────────────────────────────────────

    public void SaveManager(ManagerData manager)
    {
        if (manager.id == 0)
            _db.Insert(manager);
        else
            _db.Update(manager);
    }

    public ManagerData GetActiveManager()
    {
        return _db.Table<ManagerData>()
                  .OrderByDescending(m => m.id)
                  .FirstOrDefault();
    }

    public string GetManagerNameByTeamId(int teamId)
    {
        if (!EnsureDb()) return null;
        var mgr = _db.Table<ManagerData>().FirstOrDefault(m => m.team_id == teamId);
        if (mgr != null) return mgr.name;
        var coach = _db.Table<CoachRankingData>().FirstOrDefault(c => c.team_id == teamId && c.status != "historical");
        return coach?.name;
    }

    public void ClearAllManagers()
    {
        if (_db != null)
            _db.Execute("DELETE FROM managers");
    }

    // ── LEAGUE SETTINGS ───────────────────────────────

    public LeagueSettingsData GetLeagueSettings()
    {
        return _db.Table<LeagueSettingsData>()
              .Where(s => s.is_active == 1)
              .FirstOrDefault();
    }

    // ── SEED DATA ─────────────────────────────────────

    void SeedLeagueSettings()
    {
        _db.Insert(new LeagueSettingsData
        {
            salary_cap = TradeHelper.SALARY_CAP,
            luxury_tax = TradeHelper.LUXURY_TAX,
            apron = TradeHelper.FIRST_APRON,
            repeater_apron = TradeHelper.SECOND_APRON,
            mid_level = TradeHelper.NT_MLE,
            taxpayer_mid_level = TradeHelper.T_MLE,
            bi_annual = 5_100_000,
            minimum_salary = TradeHelper.MIN_SALARY,
            is_active = 1
        });
    }

    void SeedTeams()
    {
        var teams = new List<TeamData>
        {
            // ── ESTE — ATLÁNTICO ──
            new TeamData { name="Boston Celtics",        abbreviation="BOS", city="Boston",        conference="East", division="Atlántico",  arena="TD Garden",               capacity=19156, owner="Wyc Grousbeck",   attack=88, defense=87, overall=88, budget=310_000_000, reputation=5, facilities=5, logo="celtics",   jersey_home="celtics_home",   jersey_away="celtics_away",   salary_margin=-60_000_000, objective="Playoffs" },
            new TeamData { name="Brooklyn Nets",         abbreviation="BKN", city="Brooklyn",      conference="East", division="Atlántico",  arena="Barclays Center",         capacity=17732, owner="Joe Tsai",         attack=66, defense=65, overall=65, budget=230_000_000, reputation=2, facilities=3, logo="nets",      jersey_home="nets_home",      jersey_away="nets_away",      salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="New York Knicks",       abbreviation="NYK", city="New York",      conference="East", division="Atlántico",  arena="Madison Square Garden",   capacity=19812, owner="James Dolan",      attack=88, defense=86, overall=87, budget=310_000_000, reputation=5, facilities=5, logo="knicks",    jersey_home="knicks_home",    jersey_away="knicks_away",    salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Philadelphia 76ers",    abbreviation="PHI", city="Philadelphia",  conference="East", division="Atlántico",  arena="Wells Fargo Center",      capacity=20478, owner="Josh Harris",      attack=79, defense=78, overall=79, budget=265_000_000, reputation=3, facilities=4, logo="sixers",    jersey_home="76ers_home",     jersey_away="76ers_away",     salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Toronto Raptors",       abbreviation="TOR", city="Toronto",       conference="East", division="Atlántico",  arena="Scotiabank Arena",        capacity=19800, owner="MLSE",             attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="raptors",   jersey_home="raptors_home",   jersey_away="raptors_away",   salary_margin=-10_000_000, objective="Playoffs" },

            // ── ESTE — CENTRAL ──
            new TeamData { name="Chicago Bulls",         abbreviation="CHI", city="Chicago",       conference="East", division="Central",   arena="United Center",           capacity=20917, owner="Jerry Reinsdorf",  attack=68, defense=67, overall=67, budget=230_000_000, reputation=3, facilities=4, logo="bulls",     jersey_home="bulls_home",     jersey_away="bulls_away",     salary_margin=30_000_000,  objective="Zona tranquila" },
            new TeamData { name="Cleveland Cavaliers",   abbreviation="CLE", city="Cleveland",     conference="East", division="Central",   arena="Rocket Arena",            capacity=19432, owner="Dan Gilbert",      attack=85, defense=86, overall=86, budget=285_000_000, reputation=4, facilities=4, logo="cavaliers", jersey_home="cavaliers_home", jersey_away="cavaliers_away", salary_margin=-40_000_000, objective="Playoffs" },
            new TeamData { name="Detroit Pistons",       abbreviation="DET", city="Detroit",       conference="East", division="Central",   arena="Little Caesars Arena",    capacity=20332, owner="Tom Gores",        attack=87, defense=88, overall=87, budget=285_000_000, reputation=3, facilities=4, logo="pistons",   jersey_home="pistons_home",   jersey_away="pistons_away",   salary_margin=-45_000_000, objective="Playoffs" },
            new TeamData { name="Indiana Pacers",        abbreviation="IND", city="Indianapolis",  conference="East", division="Central",   arena="Gainbridge Fieldhouse",   capacity=17923, owner="Herb Simon",       attack=77, defense=75, overall=76, budget=255_000_000, reputation=3, facilities=3, logo="pacers",    jersey_home="pacers_home",    jersey_away="pacers_away",    salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Milwaukee Bucks",       abbreviation="MIL", city="Milwaukee",     conference="East", division="Central",   arena="Fiserv Forum",            capacity=17341, owner="Marc Lasry",       attack=75, defense=73, overall=74, budget=250_000_000, reputation=4, facilities=4, logo="bucks",     jersey_home="bucks_home",     jersey_away="bucks_away",     salary_margin=10_000_000,  objective="Play-In" },

            // ── ESTE — SURESTE ──
            new TeamData { name="Atlanta Hawks",         abbreviation="ATL", city="Atlanta",       conference="East", division="Sureste", arena="State Farm Arena",        capacity=18118, owner="Tony Ressler",     attack=81, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=3, logo="hawks",     jersey_home="hawks_home",     jersey_away="hawks_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Charlotte Hornets",     abbreviation="CHA", city="Charlotte",     conference="East", division="Sureste", arena="Spectrum Center",         capacity=19077, owner="Gabe Plotkin",     attack=74, defense=73, overall=73, budget=235_000_000, reputation=2, facilities=3, logo="hornets",   jersey_home="hornets_home",   jersey_away="hornets_away",   salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Miami Heat",            abbreviation="MIA", city="Miami",         conference="East", division="Sureste", arena="Kaseya Center",           capacity=19600, owner="Micky Arison",     attack=76, defense=77, overall=77, budget=255_000_000, reputation=4, facilities=4, logo="heat",      jersey_home="heat_home",      jersey_away="heat_away",      salary_margin=5_000_000,   objective="Play-In" },
            new TeamData { name="Orlando Magic",         abbreviation="ORL", city="Orlando",       conference="East", division="Sureste", arena="Kia Center",              capacity=18846, owner="DeVos family",     attack=78, defense=80, overall=79, budget=260_000_000, reputation=3, facilities=3, logo="magic",     jersey_home="magic_home",     jersey_away="magic_away",     salary_margin=-10_000_000, objective="Playoffs" },
            new TeamData { name="Washington Wizards",    abbreviation="WAS", city="Washington",    conference="East", division="Sureste", arena="Capital One Arena",       capacity=20356, owner="Ted Leonsis",      attack=63, defense=62, overall=62, budget=215_000_000, reputation=2, facilities=3, logo="wizards",   jersey_home="wizards_home",   jersey_away="wizards_away",   salary_margin=55_000_000,  objective="Zona tranquila" },

            // ── OESTE — NOROESTE ──
            new TeamData { name="Denver Nuggets",        abbreviation="DEN", city="Denver",        conference="West", division="Noroeste", arena="Ball Arena",              capacity=19520, owner="Ann Walton Kroenke", attack=88, defense=85, overall=87, budget=305_000_000, reputation=4, facilities=4, logo="nuggets",   jersey_home="nuggets_home",   jersey_away="nuggets_away",   salary_margin=-65_000_000, objective="Playoffs" },
            new TeamData { name="Minnesota Timberwolves",abbreviation="MIN", city="Minneapolis",   conference="West", division="Noroeste", arena="Target Center",           capacity=18978, owner="Marc Lore",        attack=83, defense=85, overall=84, budget=275_000_000, reputation=3, facilities=3, logo="wolves",    jersey_home="wolves_home",    jersey_away="wolves_away",    salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Oklahoma City Thunder",  abbreviation="OKC", city="Oklahoma City", conference="West", division="Noroeste", arena="Paycom Center",           capacity=18203, owner="Clay Bennett",     attack=90, defense=93, overall=92, budget=285_000_000, reputation=4, facilities=4, logo="thunder",   jersey_home="thunder_home",   jersey_away="thunder_away",   salary_margin=-55_000_000, objective="Campeonato" },
            new TeamData { name="Portland Trail Blazers", abbreviation="PRT", city="Portland",      conference="West", division="Noroeste", arena="Moda Center",             capacity=19393, owner="Jody Allen",       attack=74, defense=74, overall=74, budget=240_000_000, reputation=3, facilities=3, logo="blazers",   jersey_home="blazers_home",   jersey_away="blazers_away",   salary_margin=15_000_000,  objective="Play-In" },
            new TeamData { name="Utah Jazz",              abbreviation="UTA", city="Salt Lake City", conference="West", division="Noroeste", arena="Delta Center",            capacity=18306, owner="Ryan Smith",       attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="jazz",      jersey_home="jazz_home",      jersey_away="jazz_away",      salary_margin=40_000_000,  objective="Zona tranquila" },

            // ── OESTE — PACÍFICO ──
            new TeamData { name="Golden State Warriors",  abbreviation="GSW", city="San Francisco", conference="West", division="Pacífico",   arena="Chase Center",            capacity=18064, owner="Joe Lacob",        attack=79, defense=77, overall=78, budget=270_000_000, reputation=5, facilities=5, logo="warriors",  jersey_home="warriors_home",  jersey_away="warriors_away",  salary_margin=-20_000_000, objective="Play-In" },
            new TeamData { name="Los Angeles Clippers",   abbreviation="LAC", city="Los Angeles",   conference="West", division="Pacífico",   arena="Intuit Dome",             capacity=18000, owner="Steve Ballmer",    attack=75, defense=76, overall=75, budget=255_000_000, reputation=3, facilities=5, logo="clippers",  jersey_home="clippers_home",  jersey_away="clippers_away",  salary_margin=10_000_000,  objective="Play-In" },
            new TeamData { name="Los Angeles Lakers",     abbreviation="LAL", city="Los Angeles",   conference="West", division="Pacífico",   arena="Crypto.com Arena",        capacity=18997, owner="Jeanie Buss",      attack=85, defense=83, overall=84, budget=295_000_000, reputation=5, facilities=5, logo="lakers",    jersey_home="lakers_home",    jersey_away="lakers_away",    salary_margin=-50_000_000, objective="Playoffs" },
            new TeamData { name="Phoenix Suns",           abbreviation="PHX", city="Phoenix",       conference="West", division="Pacífico",   arena="Footprint Center",        capacity=18055, owner="Mat Ishbia",       attack=80, defense=79, overall=80, budget=265_000_000, reputation=3, facilities=4, logo="suns",      jersey_home="suns_home",      jersey_away="suns_away",      salary_margin=-15_000_000, objective="Play-In" },
            new TeamData { name="Sacramento Kings",       abbreviation="SAC", city="Sacramento",    conference="West", division="Pacífico",   arena="Golden 1 Center",         capacity=17608, owner="Vivek Ranadivé",   attack=69, defense=68, overall=68, budget=230_000_000, reputation=2, facilities=4, logo="kings",     jersey_home="kings_home",     jersey_away="kings_away",     salary_margin=30_000_000,  objective="Zona tranquila" },

            // ── OESTE — SUROESTE ──
            new TeamData { name="Dallas Mavericks",      abbreviation="DAL", city="Dallas",        conference="West", division="Suroeste", arena="American Airlines Center", capacity=19200, owner="Patrick Dumont",   attack=72, defense=70, overall=71, budget=245_000_000, reputation=4, facilities=4, logo="mavericks", jersey_home="mavericks_home", jersey_away="mavericks_away", salary_margin=20_000_000,  objective="Play-In" },
            new TeamData { name="Houston Rockets",       abbreviation="HOU", city="Houston",       conference="West", division="Suroeste", arena="Toyota Center",           capacity=18055, owner="Tilman Fertitta",  attack=83, defense=83, overall=83, budget=275_000_000, reputation=3, facilities=3, logo="rockets",   jersey_home="rockets_home",   jersey_away="rockets_away",   salary_margin=-25_000_000, objective="Playoffs" },
            new TeamData { name="Memphis Grizzlies",     abbreviation="MEM", city="Memphis",       conference="West", division="Suroeste", arena="FedExForum",              capacity=17794, owner="Robert Pera",      attack=72, defense=73, overall=72, budget=235_000_000, reputation=3, facilities=3, logo="grizzlies", jersey_home="grizzlies_home", jersey_away="grizzlies_away", salary_margin=25_000_000,  objective="Zona tranquila" },
            new TeamData { name="New Orleans Pelicans",  abbreviation="NOP", city="New Orleans",   conference="West", division="Suroeste", arena="Smoothie King Center",    capacity=17791, owner="Gayle Benson",     attack=67, defense=66, overall=66, budget=225_000_000, reputation=2, facilities=3, logo="pelicans",  jersey_home="pelicans_home",  jersey_away="pelicans_away",  salary_margin=35_000_000,  objective="Zona tranquila" },
            new TeamData { name="San Antonio Spurs",     abbreviation="SAS", city="San Antonio",   conference="West", division="Suroeste", arena="AT&T Center",             capacity=18418, owner="Peter Holt",       attack=88, defense=91, overall=90, budget=290_000_000, reputation=4, facilities=4, logo="spurs",     jersey_home="spurs_home",     jersey_away="spurs_away",     salary_margin=-45_000_000, objective="Campeonato" },
        };

        _db.InsertAll(teams);
        _db.Execute("UPDATE teams SET team_chemistry = 50 WHERE team_chemistry IS NULL OR team_chemistry = 0");
        _db.Execute("UPDATE players SET morale = 50 WHERE morale IS NULL OR morale = 0");
        Debug.Log($"[DB] {teams.Count} equipos insertados.");
    }

}
