docker network create timinute-database

if("$(docker ps -a | Select-string -Pattern timinute.sql.server)") {
  docker rm -f timinute.sql.server
}

docker pull mcr.microsoft.com/mssql/server:2019-latest

docker run `
  -e "ACCEPT_EULA=Y" `
  -e "SA_PASSWORD=TiminuteAdmin." `
  -e 'MSSQL_PID=Express' `
  -p 44555:1433 `
  --name timinute.sql.server `
  --hostname timinute.sql.server `
  --restart unless-stopped `
  -d `
  mcr.microsoft.com/mssql/server:2019-latest

docker network connect timinute-database timinute.sql.server