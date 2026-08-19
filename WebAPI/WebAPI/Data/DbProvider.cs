namespace WebAPI.Data
{
    /// <summary>
    /// Which relational backend the API is talking to.
    /// </summary>
    /// <remarks>
    /// The project was written against MS-SQL. SQLite exists so the public demo
    /// can run on a free container with no external database service. The SQL in
    /// the controllers is deliberately written in the subset both understand, so
    /// only the connection and the one date expression differ between them.
    /// </remarks>
    public enum DbProvider
    {
        SqlServer,
        Sqlite
    }
}
