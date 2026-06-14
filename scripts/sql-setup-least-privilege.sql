/* =============================================================================
   MitLicense Center — настройка SQL Server по принципу наименьших привилегий
   (MLC-152, ADR-28: single-host).

   Назначение: подготовить SQL Server для работы службы панели БЕЗ серверной роли
   sysadmin. Раньше sysadmin требовался единственно для бэкапа через расширенные
   процедуры (xp_fixeddrives / xp_create_subdir / xp_delete_file). По ADR-28 панель
   и SQL Server расположены на ОДНОМ узле, поэтому файловые операции бэкапа выполняет
   сама служба панели обычными вызовами .NET, а в SQL остаётся только:
     - BACKUP DATABASE / RESTORE VERIFYONLY  — входят в роль базы db_owner;
     - чтение метрик «Быстродействия»          — требует серверного GRANT VIEW SERVER STATE.

   Итог прав для учётки панели:
     - db_owner в базе данных панели  (миграции EF, обычная работа, BACKUP/VERIFYONLY);
     - VIEW SERVER STATE на сервере   (DMV для раздела «Быстродействие»).
   Роль sysadmin / dbcreator БОЛЬШЕ НЕ НУЖНА.

   Скрипт идемпотентен: повторный запуск не падает и ничего не дублирует.

   Запуск: подключиться к нужному инстансу SQL Server учётной записью, имеющей право
   создавать логины и базу (на этапе первичной настройки — администратором инстанса),
   подставить значения плейсхолдеров ниже и выполнить.

   ВАЖНО (NTFS-права на каталог бэкапов): BACKUP DATABASE пишет .bak от имени учётной
   записи СЛУЖБЫ SQL Server, а каталог создаёт и старые .bak удаляет учётная запись
   СЛУЖБЫ ПАНЕЛИ. На single-host каталог бэкапов (настройка Backup.FolderPath) должен
   быть доступен на запись/удаление ОБЕИМ учётным записям. ACL настраиваются вне этого
   скрипта (см. OPERATIONS.md «Бэкап — required permissions» / SECURITY.md «NTFS ACL»).
   ============================================================================= */

/* ---------------------------------------------------------------------------
   ПЛЕЙСХОЛДЕРЫ — подставить перед запуском:
     <DB_NAME>        имя базы данных панели            (по умолчанию: MitLicenseCenter)
     <LOGIN_NAME>     имя SQL-логина службы панели      (например: mlc_app)
     <STRONG_PASSWORD> сильный пароль SQL-логина        (НЕ коммитить реальный пароль!)

   Для Windows-аутентификации (служба под доменной/локальной учёткой) вместо SQL-логина
   используйте CREATE LOGIN [DOMAIN\\Account] FROM WINDOWS (см. блок в конце) — те же
   роли (db_owner + VIEW SERVER STATE) выдаются Windows-логину.
   --------------------------------------------------------------------------- */

DECLARE @DbName    sysname = N'MitLicenseCenter';   -- <DB_NAME>
DECLARE @LoginName sysname = N'mlc_app';            -- <LOGIN_NAME>
DECLARE @Password  nvarchar(128) = N'<STRONG_PASSWORD>';  -- <STRONG_PASSWORD>

SET NOCOUNT ON;
DECLARE @sql nvarchar(max);

/* (1) База панели — создать, если ещё нет. Миграции EF накатываются отдельно
       (scripts/db-reset.ps1 или dotnet ef database update). */
IF DB_ID(@DbName) IS NULL
BEGIN
    SET @sql = N'CREATE DATABASE ' + QUOTENAME(@DbName) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT N'База создана: ' + @DbName;
END
ELSE
    PRINT N'База уже существует: ' + @DbName;

/* (2) Серверный логин SQL-аутентификации — создать, если ещё нет.
       CHECK_POLICY = ON: пароль подчиняется политике паролей Windows. */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @LoginName)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@LoginName) +
               N' WITH PASSWORD = ' + QUOTENAME(@Password, '''') +
               N', CHECK_POLICY = ON, DEFAULT_DATABASE = ' + QUOTENAME(@DbName) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT N'Логин создан: ' + @LoginName;
END
ELSE
    PRINT N'Логин уже существует: ' + @LoginName;

/* (3) Серверное право VIEW SERVER STATE — для DMV раздела «Быстродействие».
       Не входит в db_owner (это серверное, а не уровня базы), выдаётся явно. */
SET @sql = N'GRANT VIEW SERVER STATE TO ' + QUOTENAME(@LoginName) + N';';
EXEC sys.sp_executesql @sql;
PRINT N'Выдано VIEW SERVER STATE логину: ' + @LoginName;

/* (4) Пользователь базы + членство в db_owner. Выполняется в контексте базы панели
       (динамический USE), идемпотентно. db_owner покрывает миграции EF и BACKUP/VERIFYONLY. */
SET @sql =
    N'USE ' + QUOTENAME(@DbName) + N';' + NCHAR(13) + NCHAR(10) +
    N'IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @login)' + NCHAR(13) + NCHAR(10) +
    N'    CREATE USER ' + QUOTENAME(@LoginName) + N' FOR LOGIN ' + QUOTENAME(@LoginName) + N';' + NCHAR(13) + NCHAR(10) +
    N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@LoginName) + N';';
EXEC sys.sp_executesql @sql, N'@login sysname', @login = @LoginName;
PRINT N'Пользователь добавлен в db_owner базы ' + @DbName + N': ' + @LoginName;

PRINT N'Готово. Роль sysadmin для службы панели НЕ требуется (MLC-152, ADR-28).';

/* =============================================================================
   Вариант для Windows-аутентификации (служба под Windows-учёткой).
   Раскомментировать и подставить DOMAIN\\Account; те же роли, что и выше.

   USE [master];
   IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'DOMAIN\Account')
       CREATE LOGIN [DOMAIN\Account] FROM WINDOWS
           WITH DEFAULT_DATABASE = [MitLicenseCenter];
   GRANT VIEW SERVER STATE TO [DOMAIN\Account];

   USE [MitLicenseCenter];
   IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'DOMAIN\Account')
       CREATE USER [DOMAIN\Account] FOR LOGIN [DOMAIN\Account];
   ALTER ROLE db_owner ADD MEMBER [DOMAIN\Account];
   ============================================================================= */
