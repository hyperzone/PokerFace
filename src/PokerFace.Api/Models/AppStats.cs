namespace PokerFace.Api.Models;

public class AppStats
{
    public int TotalTablesCreated { get; set; }
    public int TotalPlayersConnected { get; set; }
    public int MaxSimultaneousUsersAtTable { get; set; }
}
