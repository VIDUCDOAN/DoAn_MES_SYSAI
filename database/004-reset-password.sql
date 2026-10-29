CREATE OR ALTER PROCEDURE dbo.SYS_USER_ResetPassword
    @Username NVARCHAR(50), @PasswordHash NVARCHAR(600)
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT = 0 THROW 51002, 'Password reset requires a transaction.', 1;
    -- Giữ nguyên Id, trạng thái và các quyền; tăng phiên bản để thu hồi phiên cũ.
    UPDATE dbo.SYS_USER WITH (UPDLOCK, ROWLOCK)
    SET PasswordHash = @PasswordHash, SessionVersion = SessionVersion + 1
    OUTPUT INSERTED.Id, INSERTED.Username, INSERTED.FullName, INSERTED.IsActive, INSERTED.SessionVersion
    WHERE Username = @Username;
END;
GO
