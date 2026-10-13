# 壓縮成 tar.gz

Windows 11 x64 桌面工具。黑色簡約介面、清松手寫體 9 文青、透明壓縮盒圖示。

目前版本 **1.0.2**：所有輸出名稱加入開始壓縮時的本機日期與時間（`yyyyMMdd_HHmmss`），例如 `資料夾_20261014_153025.tar.gz`、`報告.pdf_20261014_153025.tar.gz`；多選為 `選取項目_3個_20261014_153025.tar.gz`。同秒重名仍以 `_v0.0.N` 避免覆蓋。

進度條顯示數字百分比。掃描檔案大小時為 0%；壓縮依已處理來源位元組推進 0–90%（空檔案／資料夾各計一單位）；讀回檢查依已讀壓縮檔位元組推進 90–99%；檢查通過並建立正式檔案後才顯示 100%。這是分階段工作量比例，不是剩餘時間預估。取消或失敗不會顯示 100%。

## 安裝與使用

1. 執行 `dist/Setup.exe`。這是可離線使用的安裝／管理入口。
2. 首次安裝勾選「允許管理此工具的簽章憑證」，再按「安裝」。Windows 會要求系統管理員授權，僅用於把本工具的公開簽章憑證加入本機 `TrustedPeople`。
3. 在檔案總管選取檔案或資料夾，按右鍵 → **壓縮成 tar.gz**。若選單尚未刷新，請先儲存工作，再登出並重新登入 Windows。
4. 輸出放在所選項目的共同上層目錄。多選會合併為一個壓縮檔。檔名後綴開始壓縮時的日期與時間。同名會自動加 `_v0.0.1`、`_v0.0.2` 等後綴。

右鍵功能採用原生 `IExplorerCommand` 與已簽署 MSIX 套件。Windows 會依版本及使用者選單設定決定實際顯示位置、字體和配色。壓縮視窗與此安裝程式採自訂風格，Windows 系統授權／設定視窗仍使用系統樣式。

從開始功能表開啟工具也可選取檔案；資料夾與混合多選請由檔案總管右鍵啟動。

## 更新與修復

- 執行**新版** `Setup.exe`，會辨識目前使用者已安裝的版本並升級。
- 更新前請等壓縮工作完成、關閉程式。安裝程式不會強制終止壓縮工作。
- 相同版本提供「修復註冊」，重新註冊既有套件及選單，不替換程式內容。
- 安裝程式拒絕降級。正式更新必須遞增版本號，使用同一份本機簽章金鑰。
- 更新由 Windows 部署機制處理；尚未進行真實升級／失敗回復測試。
- 第一版是手動更新，不含自動檢查更新或背景服務。此版本沒有可供下載新版本的公開發布網址。

## 解除安裝

- 執行 `Setup.exe` →「解除安裝」，或到 Windows 設定 → 應用程式 → 已安裝的應用程式 → **壓縮成 tar.gz** → 解除安裝。
- 程式與套件擁有的右鍵註冊會由 Windows 移除；不刪除使用者產生的壓縮檔。
- 若在本安裝程式中勾選憑證管理授權，會一併移除本工具的憑證信任。
- 由 Windows 設定解除安裝不會移除獨立加入的憑證信任。之後可再執行同一份 `Setup.exe`，勾選授權，按「清除憑證信任」。
- 已有的舊 `script.txt` 安裝、SendTo 捷徑與檔案不會被此版本自動刪除。

## 壓縮行為與限制

- 內建串流 TAR（POSIX ustar＋UTF-8 PAX）及 GZip，無需另外安裝 Python、Conda、.NET SDK 或壓縮工具；使用 Windows 11 內建 .NET Framework 4.8。
- 支援中文、emoji、空白、一般特殊符號、空檔案、空資料夾及隱藏項目。
- 寫入唯一暫存檔，讀回檢查後才改為正式名稱；取消／一般失敗時清除本次暫存檔。強制結束程序或斷電仍可能留下 `.peicd-targz-*.partial`，確認無工作執行後可手動移除。
- 同一次選取限共同上層目錄，不支援直接壓縮磁碟根目錄。
- 遇到符號連結或 junction 會明確報錯並停止，不會跟隨連結或略過項目。
- 保留相對路徑、檔案內容、目錄結構及秒級最後修改時間。TAR 不用於完整備份 Windows ACL、NTFS 替代資料流或其他專屬中繼資料。
- >8 GiB 單檔有 PAX size 寫入支援，但本次未用實際 >8 GiB 資料驗證。極長路徑、網路磁碟及 OneDrive 雲端未下載項目尚未實測。
- 操作中變動的資料夾不是一致性快照；檔案本身有讀取鎖定及大小／修改時間檢查，失敗會保留來源。

## 簽章與字體

此交付版使用專案本機自簽憑證，不是由公開 CA 背書的商用簽章。首次執行 Windows 可能顯示未識別發行者或 SmartScreen 提示；此程式不會關閉 Windows 保護功能。可提供給已確認來源、願意信任此簽章的使用者安裝。

憑證資訊可在安裝程式查看，也提供於 `dist/signer.cer`。私鑰僅保留在被忽略的 `build/signing/local-signing.pfx`，不隨安裝檔散布。要持續發布可直接升級的版本，請妥善離線備份此金鑰；刪除 `build/` 前先保全它。

字體由作者官方 [JasonHandwriting](https://github.com/jasonhandwriting/JasonHandwriting) 取得，未修改，依 SIL OFL 1.1 隨程式私有載入，不安裝到 Windows 字體目錄。作者及完整授權包含於套件。圖示為已確認的 AI 生成圖稿，轉成七種 ICO 尺寸。

## 建置

```powershell
# 所需 Python 套件：Pillow、fonttools、cryptography；不會自動更動 Conda 環境。
.\tools\bootstrap.ps1 -Python D:\conda\envs\PEICD100\python.exe
.\tools\build.ps1 -Version 1.0.2 -Python D:\conda\envs\PEICD100\python.exe
```

使用系統 .NET Framework C# 編譯器及專案內可攜 LLVM-MinGW／Microsoft Windows SDK BuildTools；不改系統 PATH。建置產物在 `dist/`，大型快取與私鑰不進 Git。

## 提供給別人安裝與更新

建置後執行以下命令，會重用已建置的安裝程式，產生 `dist/TarGz-1.0.2-Windows-x64.zip` 與外部 SHA-256 校驗碼：

```powershell
& D:\conda\envs\PEICD100\python.exe tools/release.py --version 1.0.2
```

ZIP 包含 `Setup.exe`、中文使用說明、公開憑證、字體作者／授權及檔案校驗碼。安裝套件已內嵌於 Setup，無需重複附上 MSIX。封裝前會檢查建置校驗碼與套件版本，完成後驗證 ZIP 清單、CRC 及所有內容；私鑰與原始碼不放入發布 ZIP。

收件者先解壓縮，再執行 `Setup.exe` 安裝。更新時取得新版 ZIP，關閉程式後執行新版 Setup；目前不含自動檢查更新。新版本須遞增版本號並保留同一份簽章金鑰。

版本下載：[GitHub Releases](https://github.com/peicd100/1_---tar.gz--/releases)。下載版本內的 `TarGz-版本-Windows-x64.zip` 即可安裝或更新；`.zip.sha256` 提供校驗碼。

可直接傳送 ZIP，或將 ZIP 與 `.zip.sha256` 附加到 GitHub Release。私人儲存庫的 Release 僅供具存取權的人下載；若需提供公開下載，可另設公開發布儲存庫。建立本機 ZIP 不會自動發布 Release。

## 驗證

```powershell
.\tests\smoke.ps1
.\tests\progress.ps1
& D:\conda\envs\PEICD100\python.exe tests/verify_package.py
.\tests\verify_signature.ps1
```

已驗證：10 項壓縮／取消／碰撞與錯誤處理、獨立 Python TAR 內容比對、同時壓縮同一來源、原生 COM 標題／狀態／生命週期／多選傳遞、介面離屏繪製、MSIX 所有區塊雜湊、CMS 簽章及簽署的 blockmap 綁定。診斷與畫面保留於 `build/`。junction 循環測試因環境拒絕建立測試連結而跳過，未宣稱通過。

**尚待實機驗收**：本次受限工作階段的 `Get-AppxPackage` 回覆「存取被拒」，未執行系統安裝、信任匯入、真實版本升級、解除安裝，亦未在 Explorer 內確認選單顯示。請在一般桌面工作階段執行 `dist/Setup.exe` 完成第一輪實機確認。
