/* Sua chu "to dan pho" con sot trong du lieu mau (dia chi ho, thong bao, phan anh) cho khop mo hinh 1 thon.
   Chay lai nhieu lan van an toan. Khong xoa dong nao. */
SET XACT_ABORT ON;
BEGIN TRAN;
UPDATE dbo.Households SET Address=
  REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Address,
  N', tổ dân phố số 1',N''),N', tổ dân phố số 2',N''),N', tổ dân phố số 3',N''),
  N' tổ dân phố số 1',N''),N' tổ dân phố số 2',N''),N' tổ dân phố số 3',N'')
WHERE Address LIKE N'%tổ dân phố%';
UPDATE dbo.Announcements SET
  Title=REPLACE(REPLACE(Title,N'tổ dân phố',N'thôn'),N'Tổ dân phố',N'Thôn'),
  Body =REPLACE(REPLACE(Body ,N'tổ dân phố',N'thôn'),N'Tổ dân phố',N'Thôn')
WHERE Title LIKE N'%ổ dân phố%' OR Body LIKE N'%ổ dân phố%';
UPDATE dbo.Requests SET
  Subject=REPLACE(REPLACE(Subject,N'tổ dân phố',N'thôn'),N'Tổ dân phố',N'Thôn'),
  Body   =REPLACE(REPLACE(Body   ,N'tổ dân phố',N'thôn'),N'Tổ dân phố',N'Thôn')
WHERE Subject LIKE N'%ổ dân phố%' OR Body LIKE N'%ổ dân phố%';
COMMIT;
SELECT TOP 5 Code,Address FROM dbo.Households ORDER BY Id;
