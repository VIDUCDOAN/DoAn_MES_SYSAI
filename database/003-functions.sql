SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @Added TABLE (FunctionCode VARCHAR(50));
INSERT dbo.SYS_FUNCTION(FunctionCode, FunctionName)
OUTPUT INSERTED.FunctionCode INTO @Added
SELECT FunctionCode, FunctionName FROM (VALUES
    ('MODEL_VIEW', N'Xem sản phẩm'), ('MODEL_ADD', N'Thêm sản phẩm'),
    ('MODEL_EDIT', N'Sửa sản phẩm'), ('MODEL_DELETE', N'Ngừng sử dụng sản phẩm'),
    ('AUDIT_VIEW', N'Xem nhật ký thay đổi')
) AS Source(FunctionCode, FunctionName)
WHERE NOT EXISTS (SELECT 1 FROM dbo.SYS_FUNCTION WITH (UPDLOCK, HOLDLOCK) WHERE FunctionCode = Source.FunctionCode);
IF EXISTS (SELECT 1 FROM @Added)
BEGIN
    DECLARE @NewJson NVARCHAR(MAX) = (SELECT FunctionCode FROM @Added FOR JSON PATH);
    INSERT dbo.SYS_AUDIT_LOG(Who, [When], WhereIp, ActionType, EntityName, EntityId, OldDataJson, NewDataJson, CorrelationId)
    VALUES (N'System', SYSUTCDATETIME(), N'Local', 'INSERT', 'SYS_FUNCTION', N'bootstrap', NULL, @NewJson, N'database-003');
END;
COMMIT;
