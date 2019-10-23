Trying to repro issue

https://github.com/dotnet/SqlClient/issues/104


To run this, use dotnet sdk 3.0

dotnet run <server name> <user> <password>

This will queue 300 tasks to open the connections and close them. Use `netstat -nat ` to see all open TCP connections to the Sql Server. There should be only one connection in established state, however I can see 5-10 at this scale.

