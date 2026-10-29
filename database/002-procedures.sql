CREATE OR ALTER PROCEDURE dbo.SYS_Health
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) AS FunctionCount FROM dbo.SYS_FUNCTION;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SYS_USER_GetByUsername @Username NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, PasswordHash, FullName, IsActive, SessionVersion
    FROM dbo.SYS_USER WHERE Username = @Username;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SYS_USER_GetSession @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Username, FullName, IsActive, SessionVersion FROM dbo.SYS_USER WHERE Id = @UserId;
    SELECT FunctionCode FROM dbo.SYS_PERMISSION WHERE UserId = @UserId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SYS_USER_Bootstrap
    @Username NVARCHAR(50), @PasswordHash NVARCHAR(600), @FullName NVARCHAR(200), @CanManage BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Bootstrap requires a transaction.', 1;
    IF EXISTS (SELECT 1 FROM dbo.SYS_USER WITH (UPDLOCK, HOLDLOCK) WHERE Username = @Username) RETURN;
    INSERT dbo.SYS_USER(Username, PasswordHash, FullName) VALUES (@Username, @PasswordHash, @FullName);
    DECLARE @UserId INT = CONVERT(INT, SCOPE_IDENTITY());
    INSERT dbo.SYS_PERMISSION(UserId, FunctionCode)
    SELECT @UserId, FunctionCode FROM dbo.SYS_FUNCTION
    WHERE @CanManage = 1 OR FunctionCode = 'MODEL_VIEW';
    SELECT @UserId;
END;
GO
CREATE OR ALTER PROCEDURE dbo.M_PRODUCT_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (200) Id, ModelCode, ModelName, Description, IsDeleted, Version, CreatedAt
    FROM dbo.M_PRODUCT WHERE IsDeleted = 0 ORDER BY Id DESC;
END;
GO
CREATE OR ALTER PROCEDURE dbo.M_PRODUCT_GetForUpdate @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Update requires a transaction.', 1;
    SELECT Id, ModelCode, ModelName, Description, IsDeleted, Version, CreatedAt
    FROM dbo.M_PRODUCT WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;
END;
GO
CREATE OR ALTER PROCEDURE dbo.M_PRODUCT_Create
    @ModelCode VARCHAR(50), @ModelName NVARCHAR(200), @Description NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Insert requires a transaction.', 1;
    INSERT dbo.M_PRODUCT(ModelCode, ModelName, Description)
    OUTPUT INSERTED.Id, INSERTED.ModelCode, INSERTED.ModelName, INSERTED.Description,
        INSERTED.IsDeleted, INSERTED.Version, INSERTED.CreatedAt
    VALUES (@ModelCode, @ModelName, @Description);
END;
GO
CREATE OR ALTER PROCEDURE dbo.M_PRODUCT_Update
    @Id INT, @ModelCode VARCHAR(50), @ModelName NVARCHAR(200), @Description NVARCHAR(1000), @Version INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Update requires a transaction.', 1;
    UPDATE dbo.M_PRODUCT SET ModelCode = @ModelCode, ModelName = @ModelName,
        Description = @Description, Version = Version + 1
    OUTPUT INSERTED.Id, INSERTED.ModelCode, INSERTED.ModelName, INSERTED.Description,
        INSERTED.IsDeleted, INSERTED.Version, INSERTED.CreatedAt
    WHERE Id = @Id AND Version = @Version AND IsDeleted = 0;
    IF @@ROWCOUNT = 0 THROW 51001, 'Stale product version.', 1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.M_PRODUCT_Delete @Id INT, @Version INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Delete requires a transaction.', 1;
    -- Xóa mềm: giữ lịch sử và mã duy nhất để các module sản xuất sau này còn truy vết.
    UPDATE dbo.M_PRODUCT SET IsDeleted = 1, Version = Version + 1
    OUTPUT INSERTED.Id, INSERTED.ModelCode, INSERTED.ModelName, INSERTED.Description,
        INSERTED.IsDeleted, INSERTED.Version, INSERTED.CreatedAt
    WHERE Id = @Id AND Version = @Version AND IsDeleted = 0;
    IF @@ROWCOUNT = 0 THROW 51001, 'Stale product version.', 1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SYS_AUDIT_LOG_Insert
    @Who NVARCHAR(50), @When DATETIME2(7), @WhereIp NVARCHAR(45), @ActionType VARCHAR(20),
    @EntityName VARCHAR(100), @EntityId NVARCHAR(100), @OldDataJson NVARCHAR(MAX),
    @NewDataJson NVARCHAR(MAX), @CorrelationId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Audit requires the business transaction.', 1;
    INSERT dbo.SYS_AUDIT_LOG(Who, [When], WhereIp, ActionType, EntityName, EntityId, OldDataJson, NewDataJson, CorrelationId)
    VALUES (@Who, @When, @WhereIp, @ActionType, @EntityName, @EntityId, @OldDataJson, @NewDataJson, @CorrelationId);
END;
GO
CREATE OR ALTER PROCEDURE dbo.SYS_AUDIT_LOG_List
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (200) Id, Who, [When], WhereIp, ActionType, EntityName, EntityId, OldDataJson, NewDataJson, CorrelationId
    FROM dbo.SYS_AUDIT_LOG ORDER BY Id DESC;
END;
GO
