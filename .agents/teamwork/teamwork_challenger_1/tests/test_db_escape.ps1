# Test SQL injection via unsanitized bracket escaping in database name
$DatabaseName = "LMS]; SELECT 1;--"
$checkSql = "DBCC CHECKDB ([$DatabaseName]) WITH NO_INFOMSGS, ALL_ERRORMSGS;"
Write-Host "Constructed SQL: $checkSql"

# In SQL Server, closing brackets MUST be escaped as ]]
$escaped = $DatabaseName.Replace("]", "]]")
$safeSql = "DBCC CHECKDB ([$escaped]) WITH NO_INFOMSGS, ALL_ERRORMSGS;"
Write-Host "Safely Escaped SQL: $safeSql"
