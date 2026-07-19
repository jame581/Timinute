docker network create timinute-database

if("$(docker ps -a | Select-string -Pattern timinute.sql.server)") {
  docker rm -f timinute.sql.server
}

docker pull mcr.microsoft.com/mssql/server:2025-latest

# Single knob for the SA password: honor MSSQL_SA_PASSWORD (same var docker-compose
# uses) and fall back to the built-in dev default so `dotnet run` works out of the box.
$saPassword = if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { "TiminuteAdmin." }

docker run `
  -e "ACCEPT_EULA=Y" `
  -e "MSSQL_SA_PASSWORD=$saPassword" `
  -e "MSSQL_PID=Express" `
  -p 44555:1433 `
  --name timinute.sql.server `
  --hostname timinute.sql.server `
  --restart unless-stopped `
  -d `
  mcr.microsoft.com/mssql/server:2025-latest

docker network connect timinute-database timinute.sql.server
