using SQLite;

/// <summary>Metadatos pequeños del template y del slot; no son estado de temporada.</summary>
[Table("db_meta")]
public class DbMetaData
{
    [PrimaryKey]
    public string key { get; set; }

    public string value { get; set; }
}
