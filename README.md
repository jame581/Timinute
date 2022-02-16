# Timinute
[![Build & Test](https://github.com/jame581/Timinute/actions/workflows/build_test.yml/badge.svg?branch=master)](https://github.com/jame581/Timinute/actions/workflows/build_test.yml)

The application serves as a demonstration of the possibilities of the new Blazor technology. Allows you to track time and task. Identity server is used to manage users.

### Prerequisites

Software what you need is:

* [Visual Studio 2022 (Version 17 or better)](https://visualstudio.microsoft.com/)
* [.NET 6.0 SDK (v6.0.0 or better)](https://dotnet.microsoft.com/download/dotnet)
* [Docker Desktop (Is used for SQL Database)](https://www.docker.com/get-started) 

That's it :)

## Getting Started

First step install prerequisites software, then clone repository, setup SQL database, build solution, then run database migrations and finally run solution.

* Clone repository
* Go to folder `Scripts`
  * run script `SetupDockerSql.ps1`
  * run script `MigrateDatabase.ps1`
* Build & run solution

## Running the tests

Test will be added in future.

## Author

* **Jan Mesarč** - *Creator* - [jame581](https://jame581.azurewebsites.net/)
