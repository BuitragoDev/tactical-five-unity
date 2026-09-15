using System;

[Serializable]
public class SaveSlotInfo
{
    public int slotNumber;
    public bool exists;
    public string managerName;
    public string teamName;
    public string teamLogo;
    public string seasonYear;
    public string currentDate;
    public string lastPlayedRealDate;
    public int currentGameDay;
    public string gameMode;

    // LoadGame card data
    public int teamId;
    public long teamBudget;
    public long teamPayroll;
    public int teamChemistry;
    public string managerStatus;
}
