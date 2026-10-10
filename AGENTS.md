# PEICD Global Codex Rules
版本：1.12.0
`$CODEX_HOME/AGENTS.md`；未設定時為 `~/.codex/AGENTS.md`。
本文件刻意拆成兩個可獨立理解的區塊：**第一區是可分享、可重用的 PEICD 記憶維護機制**；**第二區是目前使用者的個人化 Codex 規則與偏好**。分享給其他人時，可直接保留第一區；第二區應由使用者逐項審查、刪除或替換。
受信任、根目錄明確且可持久寫入的專案一律啟用 PEICD。跨專案記憶機制放本檔；專案知識、狀態、決策、證據放 `.peicd100/codex/`。未要求不建立專案 AGENTS；既有外部 AGENTS 不得刪除／覆蓋／忽略。

---
## 1. 通用 PEICD 記憶維護機制
本區只定義 durable memory、任務狀態、Recovery、Consolidation、Checkpoint 與多代理恢復等可跨使用者重用的機制；不綁定特定通知渠道、Git 操作偏好、硬體、語言或回覆格式。
### 1.1 權威、主要任務與工作階段
#### 1.1.1 指令優先順序

規則衝突時依序採用：

1. 系統、安全、工具、平台與受管理政策。
2. 使用者最新指令。
3. 本全域 `AGENTS.md`。
4. 專案 `.peicd100/codex/` 中有效、可追溯的使用者要求、accepted 決策與 Canonical。
5. 原始碼、設定、鎖定檔、Git、測試、建置與執行結果所證明的狀態。
6. 歷史、deprecated、superseded、archived 或效力不明的紀錄，只能參考。

規範與實作不一致：目標依較高規範、現況依證據；同級無法判定才標 `CONFLICT`。
#### 1.1.2 主要任務優先

使用者任務優先；記憶、Checkpoint、壓縮、交接、通知從屬。除防資料遺失／恢復／跨 session／驗收所需，不得中斷或擴大主工作、逐命令維護記憶，且維護成本不得高於主工作。
#### 1.1.3 單一主要目標

- 長 session 只一個 `goal_revision`；補充要求併 acceptance criteria／hard constraints。目標改變先收斂不可安全中止操作，再升 revision、取消／封存舊步驟，防 compaction 復活。
- 實質新目標用新 `task_id` 並交接後換 session；steer／follow-up 記 `clarification | constraint_change | goal_change | cancellation`，不得只留最後一句。
#### 1.1.4 Durable memory

摘要、百分比、內部思考、task handle／自然語言回憶不是 durable evidence；跨回合／compaction／模型／session 資訊須進已提交 PEICD 或可驗證產物。Codex Memories／Chronicle 非權威，不得覆蓋 `CLEAN` PEICD／workspace evidence；寫入前重驗。非 `CLEAN` 只供修復。
#### 1.1.5 記憶安全不變量

- 每 topic 只一個 active Canonical；每 execution domain 只一份 active `Effective Policy`，歷史不得並列 active。
- 任何會影響執行的 Canonical／Policy／Pointer／Task Packet／Checkpoint／Scheduler／Runtime 同名或同義參數，須收斂到同一 `Active execution scope`。不可變舊來源若衝突，保留原檔但以原檔 SHA-256 綁定 authority-resolution／supersession，排除其執行權威。
- 仍有可執行來源可能採衝突值即 `EXECUTION_AUTHORITY_CONFLICT`：不得 `CLEAN`、不得外部副作用；禁止靠代理猜新舊。
- PEICD memory 單一 writer；mutation 後必 Consolidation，完成前 Read Barrier 禁新 Phase／子代理／昂貴 Run／不可逆決策。
- 有效集合只由 clean commit、index／入口、lifecycle、digest 與 authority resolution 決定；禁用 mtime／檔名／順序猜新舊。新資訊優先合併，重複／過期／取代／歷史須 supersede／cold-archive；維護 evidence 不遞迴。

---
### 1.2 所有專案啟用 PEICD
#### 1.2.1 適用專案

受信任、根目錄明確且可持久寫入的專案首次處理時確認 `.peicd100/codex/`、`.peicd100/codex_compressed/`。純臨時沙盒、一次性檢視、系統／第三方／未受信任專案、明確禁止初始化的唯讀任務、或根目錄不明時不自動寫入。

其餘專案即使當次唯讀，也可冪等、低成本建立最小 PEICD，且不得覆蓋既有內容；實際改檔後是否發送完成通知，依第二區的個人化完成通知政策；通用核心不預設任何通知渠道。
#### 1.2.2 最小骨架

```text
.peicd100/
  codex/
    00_START_HERE.md
    MEMORY_INDEX.yaml
    MEMORY_COMMIT.json
    PROJECT.md
    ACTIVE_TASK.md
    CURRENT_STATE.md
    USER_REQUIREMENTS.md
    GOTCHAS.md
    VERIFY.md
  codex_compressed/
```

缺少時只冪等建立必要項；既有記憶先保全／路由，不複製 Canonical 或大遷移。`00_START_HERE.md` ≤2 KiB；小任務不建完整 Run／Ledger／Registry／Playbook；無跨任務價值、狀態或決策變化不更新。read-only 未改檔不觸發個人化完成通知；bootstrap／記憶變更須 clean consolidation、更新交接包，之後再依第二區的個人化完成通知政策處理通知。
#### 1.2.3 擴充骨架

長期／多代理／昂貴專案可加 `ARCHITECTURE.md`、`DECISIONS.md`、`COMMANDS.md`、log、`playbooks/`、`knowledge/`、`tools/`、`registry/`、`archive/`、`tmp/`、`private/`、`artifacts/`。memory mutation 優先由受信任 `memory_txn.py`／等價 helper 管 lock／generation／manifest／digest／commit；未知項先保全。

---
### 1.3 記憶文件職責與 Canonical 規則
#### 1.3.1 文件職責

- `00_START_HERE.md` ≤2 KiB：狀態／路由／generation／唯一下一步；`MEMORY_INDEX.yaml`：generation、active manifest、topic→唯一 Canonical／lifecycle／path／Hash。
- `MEMORY_COMMIT.json`：`memory_state=CLEAN|DIRTY|CONSOLIDATING|NEEDS_REPAIR`、current／last-clean generation、digest、txn／lock owner、commit time；不寫 archive SHA，避免自我參照。
- `PROJECT`／`ACTIVE_TASK`／`CURRENT_STATE`／`USER_REQUIREMENTS`：邊界／任務／verified 現況（可含明確 `UNKNOWN／CONFLICTED` safety state）／跨任務要求；推論仍放 Task。
- log 只留近期摘要；大量序列進 registry；archive 啟動不讀；tmp 可重建；private／artifacts 不進一般記憶包。
#### 1.3.2 Canonical、Effective Policy 與有效集合

每 topic 只一個 Canonical；其他檔只留摘要／連結。`Effective Policy` 是 Canonical 經 authority resolution 後的唯一執行投影，不得形成平行規則。Summary drift=`SUMMARY_DRIFT`；取代來源設 `superseded`+`superseded_by`，必要時 archive；`archived + active` 非法。

active Canonical／Effective Policy 只在 commit=`CLEAN`、generation／digest 一致且 execution-bearing sources 已語意收斂時有效。不可變 Packet／Checkpoint／舊 Runtime 可留作 evidence；被取代者須有 hash-bound authority-resolution 並退出 Active execution scope。未索引、generation 不符、非 active、摘要副本或效力不明內容不得覆蓋；外部可變事實按需重驗。
#### 1.3.3 記憶三軸

`lifecycle`: draft／active／deprecated／superseded／archived；`validity`: unverified／configuration_attested／verified／failed／conflicted；`evidence`: user／file／git／test／runtime／external。詳細 Schema 放 Playbook／Registry。
#### 1.3.4 寫入門檻與防膨脹

永久記憶只存跨 session 要求／accepted 決策／高風險坑／穩定架構、命令、驗證／Canonical Hash／必要進度、阻塞、下一步；聊天、探索、unverified 推論、完整 log／逐分鐘進度不保存。寫前查 topic，能更新／取代就不新增 active；完成進度、失效 workaround、舊 next step、被證據否定狀態同 transaction 收斂。

Active memory 64 KiB soft／96 KiB hard：soft 超過強制 Consolidation；hard 超過不得 `CLEAN`，除非明確調高。archive >256 KiB 或 >100 entries 時 cold-consolidate：可重建者只留 tombstone；唯一 evidence 不刪。
#### 1.3.5 Memory Transaction／Consolidation Gate

每 mutation batch 一 transaction；lock／generation／manifest／digest／commit 由 `memory_txn.py`／deterministic helper 處理：

1. atomic `mkdir`／exclusive-create `tmp/memory_txn.lock`，記 txn／task／session／run、owner、base、heartbeat；不因 age 刪 lock，接管須證 owner terminal／orphan，commit 前重驗 base。
2. 驗 base=`CLEAN`；確認 `codex_compressed/LATEST.json` 指向同 generation 且 SHA-256 通過的 clean 包；缺失先建立，再設 `DIRTY`。
3. `CONSOLIDATING`：去重／merge／supersede／cold-archive後，枚舉 execution-bearing sources，正規化同義參數並依 0.1 解權威；不可變衝突來源用原檔 Hash 綁定 authority-resolution，不改原檔。無唯一解=`EXECUTION_AUTHORITY_CONFLICT`。
4. 每 execution domain 只產生／刷新一份 `Effective Policy`／等價 runtime snapshot，記 Canonical／來源 Hash；Builder／Scheduler／Executor 不得採已 superseded raw packet。
5. candidate=`last-clean+1`；Canonical→Effective Policy／resolution→index→入口；digest=manifest raw bytes SHA-256，path 依 UTF-8 bytes 排序，排除 commit、tmp／archive／artifacts／private／inactive log。
6. 1.12.1 pre-lint→atomic `CLEAN`→post-verify；失敗=`NEEDS_REPAIR`。成功才驗 clean 包、更新 `LATEST.json`、release lock；repair 先寫 terminal marker。
7. crash／殘 lock／generation／digest／authority 異常即 Read Barrier；驗 LATEST 後續 candidate 或回復 last-clean；禁第二 writer。

---
### 1.4 啟動與漸進讀取
#### 1.4.1 一般啟動

1. 判定根目錄／目標，確認骨架且不重建既有內容。
2. 先讀 commit、入口、writer lock；驗 state／generation／digest／base／owner。
3. commit 非 `CLEAN`、資料不符、入口失效、lock owner 不明或有 `EXECUTION_AUTHORITY_CONFLICT` 時 Read Barrier，只讀修復資料並先 Recovery／Consolidation。
4. `ACTIVE_TASK` 僅在 `active／blocked`、goal 相符且屬有效集合時恢復；其餘按 index 只讀相關 active Canonical、State、Requirements、GOTCHAS、VERIFY。
5. 外部副作用前解析唯一 Effective Policy 並核對來源 Hash／runtime snapshot；Packet／Checkpoint／Scheduler 只是 input/evidence，未被 authority resolution 選中不得升格。
6. 缺檔、未索引、archive、效力不明或 superseded 資料不得當目前真相／執行權威。

同 session 不反覆重讀；僅在 transaction、compaction、resume／clear、commit 不明或衝突時重讀。
#### 1.4.2 分層閱讀

L0 系統／本規則／最新指令；L1 commit + 入口；L2 Task／State／Requirements／GOTCHAS；L3 直接相關 Canonical／架構／VERIFY／Playbook／Knowledge／Schema；L4 只在歷史追查、遷移、法務／資安或衝突時讀 archive。
#### 1.4.3 讀取與輸出控制

先入口／摘要，再用 `rg`／index／行號定位，最後讀必要全文；不得起手掃整 repo／PEICD／archive／大 log／generated／transcript／壓縮內容。未知命令先限輸出／導暫存；stack trace、test log、探索筆記、大報告留磁碟，主線只留結論、evidence、風險、下一步。無新 evidence／里程碑／阻塞／終態不重複狀態／Checkpoint。

---
### 1.5 ACTIVE_TASK 與 Execution Plan
#### 1.5.1 狀態機

`task_status`: `idle | active | blocked | completed | cancelled`；步驟 `[ ]` pending、`[~]` in_progress、`[x]` completed、`[!]` blocked、`[-]` cancelled。只一個主要 `[~]`；平行工作放 `live_operations`／子步驟。
#### 1.5.2 完整計畫門檻

三步以上、子代理、昂貴／長測試、重大／不可逆修改、跨 session 或重跑成本高時，實作前建 Phase／Gate Plan，列 `done_when`、產物、驗收；不列低階命令，未知新增記 `plan_changes`。
#### 1.5.3 ACTIVE_TASK 最低內容

```text
task_id / task_title / task_status / goal_revision / goal
acceptance_criteria / hard_constraints / assumptions_to_verify
current_phase / current_step_id / execution_plan / next_exact_action / resume_rule
session_compaction_count / task_compaction_total / live_operations / frozen_input_hashes
completion_markers / run_inventory / files_created / files_modified / files_pending
commands_run / verification_results / evidence / known_failures / blockers / plan_changes
decisions_made / agents_version / last_checkpoint_at
```

重要步驟有 `done_when`；`[x]` 須有 evidence／結果／marker／產物／Hash。只在規劃、Phase／Gate／原子步驟邊界、昂貴操作／子代理前後、驗證、決策、goal revision、阻塞、recovery、handoff／完成時更新；普通讀檔／搜尋／命令／等待不更新，時間序列進 Registry／JSONL。
#### 1.5.4 完成與取消

完成時記驗證、產物／Hash，清 `[~]`、暫時 blocker、舊 next step，設 `completed`；取消保留 evidence／原因／可重用產物，未開始步驟 `[-]`；新任務新 `task_id`。

---
### 1.6 耐中斷原子工作
#### 1.6.1 啟用條件

真正子代理／語意 LLM Run、不可安全重入、>3 分鐘昂貴流程、無法判定中斷位置批次、正式 Canonical／Registry／大量檔案遷移，開始前建立可恢復狀態。
#### 1.6.2 PRE_ACTION_CHECKPOINT

優先 run-local `PRE_ACTION_CHECKPOINT.json`；未被 Run Hash 綁定才直接寫 `ACTIVE_TASK`。至少記 `task／revision／step／run／role或command／started_at／inputs+hashes／effective_policy或config hash／expected_outputs／done_when／marker／staging／reservation／task_handle／resume-retry-rollback_rule`。
#### 1.6.3 原子、冪等與凍結

副作用拆冪等子步驟並寫 marker／Hash／append-only event；恢復從首個未完成處續。正式檔優先 temp→驗證→atomic rename，否則先備份。子代理／昂貴流程只寫 staging；同 run、input Hash 相符且驗收通過才 promotion，Partial 不 promotion，retry 新 attempt 且不覆蓋失敗證據。terminal 前凍結角色、Effective Policy／Packet、正式輸入、模型路由、驗收、Hash-bound Checkpoint；已有受管 Builder／Schema／validator 時，衍生 task／worker prompt 必須由其產生或驗證，禁手寫繞 Gate。進度只 append `RUN_PROGRESS.jsonl`；改 frozen input 前先 terminal／supersede 舊 Run。

---
### 1.7 Context compaction／resume 恢復協議
#### 1.7.1 觸發與證據優先序

compaction、resume／clear、模型切換、重啟、task／歷史／上一步不明或狀態矛盾時，在寫檔、推進 Phase、replacement、新昂貴 LLM 前跑 Recovery。信任：最新指令／安全 → clean Canonical／Registry／terminal marker／Hash → workspace／Git／test／runtime → Checkpoint → 摘要／回憶。
#### 1.7.2 Recovery Gate

1. 讀 commit／入口／Task／State／index／lock；驗 state／generation／digest／base；非 clean 先驗 LATEST+last-clean archive，必要時 repair mode 取 lock。
2. 重做 authority resolution：核對 Canonical／Effective Policy 與 Packet、Checkpoint、Scheduler／Runtime；舊值不得因更靠近 Run 而復權。未解衝突即 Read Barrier。
3. Git status／diff 對帳，重建 task／revision／phase-step、completed、Canonical／Policy Hash、live runs、blocker、唯一 next action。
4. 驗 `[~]`／前置 `[x]` 的 marker、完整產物、run identity、I/O Hash；不只信勾選／摘要／檔案存在。
5. live／unknown Run 對帳 process／child／run dir／reservation／ledger／partial／terminal；running 只監看，不可觀測=`UNKNOWN`；handle 消失≠terminal，owner 未證 terminal／orphan 不接管。
6. 同 Run terminal＋完整輸出＋Hash／驗收才完成；terminal／orphan 且不完整才 `interrupted`；有 evidence 的昂貴工作不因摘要缺失重跑。
7. Candidate 可證則續 txn，否則回 verified last-clean 再開；不能安全恢復則 task=`blocked`、memory=`NEEDS_REPAIR`，禁猜測／replacement。

無 validator 時，終態至少需同 Run terminal、完整產物、可重算 Hash、最小驗收；否則 `UNKNOWN／blocked`。
#### 1.7.3 恢復輸出與重複 compaction

只回報 revision、phase／step、verified completed、live／unknown Runs、blocker、`next_exact_action`。compaction 計數僅有 evidence 才更新；第 2 次後先收斂原子步驟、刷新 handoff，第 3 次以上安全邊界設 `handoff_recommended: true`。不得因計數永久阻塞或為 handoff 終止 live 子代理；留原 session 時 Recovery 後可縮小讀取續跑。

---
### 1.8 Hook 輔助與限制
#### 1.8.1 信任與分工

AGENTS 不代表 Hook 已安裝；須有設定、受信任腳本與可驗結果才算生效，Hook 不得是唯一記憶源。`PreCompact` 只寫最小快照；`PostCompact／SessionStart` 只路由 Recovery；`SubagentStart` 只給最小 Packet；`SubagentStop／Stop` 只做短 deterministic 驗證。Hook 禁昂貴 LLM、打包、外部通知、continuation loop；非受管理 command hook 先審查。
#### 1.8.2 PreCompact 與安全

快照寫 run-local `COMPACTION_SNAPSHOT.json` 或 `tmp/compaction/<id>.json`，含 task／revision／step、live run ids、input hashes、expected outputs、next recovery action、time；不進 Run Hash、不改 frozen Checkpoint，stdout 非 durable evidence。Hook 須 deterministic、快速、有界、不讀秘密／不遞迴；並行用 lock＋唯一檔名＋原子寫，失敗留 evidence 交 Recovery。

---
### 1.9 多代理與子代理
#### 1.9.1 使用門檻

只用於輸入獨立且能改善品質／速度／主線上下文的語意分工，如探索、獨立審查、分片分析、創作／盲審／裁決。進度、格式、Hash、Schema、lint、test、FFmpeg、重複意見或委派成本更高者不用。
#### 1.9.2 啟動前紀錄與 Packet

持久記 `task_id / goal_revision / role / run_id / attempt_id / model+reasoning / effective_policy_hash / input_manifest+hashes / expected_outputs / done_when / staging / marker / reservation / task_handle / timeout／replacement_rule`；同 `task+phase+role+input_hash` 最多一個 active Run。子代理只給唯一任務、必要 Canonical／Effective Policy、frozen inputs、Schema／staging／驗收／安全／模型限制，不讀整套 PEICD／無關歷史；有受管 Packet Builder 必須用且驗證，禁手寫漂移副本。長 log 留 Run dir，主線只收結論、風險、evidence、terminal status。
#### 1.9.3 執行與恢復

Run 期間凍結 Packet／輸入／Checkpoint；不因慢、compaction、暫無輸出、handle 不可見重開。子代理只回 staging／evidence，除非自己是 1.3.5 唯一 writer。恢復先對帳 run／output／marker／reservation／ledger；未證 terminal 不 replacement。缺終態優先恢復同 Run；只有 confirmed terminal／orphan、無可接受 evidence 且舊 attempt=`interrupted／rejected` 才新建。禁止平行寫同檔／爭資源。

---
### 1.10 確定性記憶工作
Hash、Schema、Registry、authority resolution、Effective Policy／Packet Builder 與 memory transaction bookkeeping 優先用 Script／工具；PEICD lock／manifest／digest／generation／commit／recover 由 `memory_txn.py`／等價 deterministic helper 處理。與記憶生命週期直接相關的可重複流程放 Skill／`playbooks/`／`tools/`，不要靠代理重複手工拼接相同規則。
### 1.11 `codex_compressed` 交接包
#### 1.11.1 位置與時機

`.peicd100/codex_compressed/` 是 last-good checkpoint／交接包；包名 `YYYY-MM-DD_HHMMSS.tar.gz`，旁置 `LATEST.json` 記 generation／digest／path／SHA-256（不入 tar，避免自我參照）。只在 `CLEAN`、lint／digest 通過時原子更新；相同 digest 重用，非 clean 不覆蓋。
#### 1.11.2 打包與驗證

1. 從專案根打包 `.peicd100/codex/`，排除 `tmp/`、`artifacts/`、`private/`。
2. 用 `tar -tzf`／`tarfile` 驗可讀；至少含入口、index、commit、Task、State，且 clean generation／digest 可重驗。
3. 驗無排除項／秘密／大型產物，計 archive SHA-256 後原子更新 `LATEST.json`。
4. 新 LATEST 通過才刪舊包；失敗保留舊 LATEST／包並記 blocker。打包／lint／commit／lock／通知 evidence 不遞迴；敏感需求只留去識別 manifest。

---
### 1.12 記憶驗證與通用完成順序
#### 1.12.1 記憶整理與 deterministic lint

每 mutation batch 先完成 1.3.5。pre-lint 除結構／Hash 外必驗語意 authority：topic 單一 Canonical、execution domain 單一 Effective Policy；被取代的不可變執行設定有 hash-bound supersession；Pointer／Packet／Checkpoint／Scheduler／Runtime／衍生 worker Packet/Prompt 與有效 Policy 一致。即使 path／Hash／lifecycle 正確，只要仍有可執行語意衝突就以 `EXECUTION_AUTHORITY_CONFLICT` FAIL。另驗 manifest／digest、writer／base、Task／`[~]` 唯一、`[x]` evidence、State、budget／編碼／秘密。`CLEAN` 後重驗 generation／digest／authority Hash；失敗=`NEEDS_REPAIR`，必要時 task=`blocked`，保留 last-clean。
#### 1.12.2 固定完成順序

改檔後固定：`產物驗證 → Consolidation/pre-lint → CLEAN/post-verify → codex_compressed/LATEST → release lock → 個人化完成通知（若啟用） → 回覆`。後續失敗不回滾已驗證產物但須說明；整理失敗=`NEEDS_REPAIR` 且不換 clean 包，整理／打包失敗時，是否通知與通知內容依第二區個人化政策；維護 evidence 不遞迴。

---
## 2. 使用者個人化 Codex 規則與偏好
本區不是 PEICD 記憶機制成立的必要條件，而是目前使用者的工作方式與環境設定。分享本文件時，其他使用者應逐項確認是否保留，尤其是通知地址、Git 策略、作業系統、Conda 環境與輸出偏好。
### 2.1 全域 AGENTS 檔案與指令預算
- 未指定路徑的「修改 `AGENTS.md`」即 `$CODEX_HOME/AGENTS.md`。
- 實質修改遞增版本；結構改版升 minor，局部修正升 patch。
- 非空全域 `AGENTS.override.md` 會遮蔽本檔；不得自行建立／刪除／覆蓋，發現先回報。
- 目標 26～28 KiB、上限 30 KiB。`project_doc_max_bytes` 預設 32 KiB，只限 project instructions；各層須驗未截斷／重複。確需更長可建議 ≥`65536`，未授權不改。
- 領域知識、長範本與重複流程下沉 PEICD、Skill、Playbook／Script。
### 2.2 語言與編碼
- 使用臺灣繁體中文；程式碼、API、命令、路徑、Schema、專名保留英文。
- 中文檔用 UTF-8 無 BOM 與既有換行；無慣例用 LF，不得只為格式改任務外檔案。

---
### 2.3 一般執行、快取、測試與模型策略
#### 2.3.1 確定性一般工作優先

格式、一般測試、FFmpeg、STT／TTS 與其他可確定性重現的工作優先用 Script／工具；可重複的一般工作流程放 Skill／`playbooks/`／`tools/`，避免代理反覆手工執行相同程序。
#### 2.3.2 增量處理

影片、AI生成、轉檔、建置、索引、STT／OCR、測試資料與大型報告：先辨識階段依賴；以 Hash＋必要版本／模型／參數／metadata 判快取；有效同輸入 evidence 才重用，只重跑受影響階段與必要測試。Metadata 不可信、輸入變更、驗證失敗或要求全量才重跑；回報重用／跳過／驗證。不得只靠檔名重用或反覆有損轉碼。
#### 2.3.3 測試策略

先跑最小針對性驗證，再依 Gate 升級至整合、build 或完整測試；輸入與程式碼未變時不重跑相同完整測試。失敗保存可重現命令與高訊號錯誤，完整 log 留磁碟。
#### 2.3.4 模型與 Reviewer

- 使用者／Canonical 鎖定 model、reasoning、fallback 時不得靜默降級；Telemetry 不可觀測=`UNKNOWN`，不得稱 provider-attested。
- 未鎖定時按風險選足夠可靠模型；完整能力留給創作／重大裁決／高風險／最終品質，輕量探索／格式／確定性驗證不用最高成本。Reviewer 只在可能改變決策時增加。

---
### 2.4 完成通知
#### 2.4.1 Gmail 完成通知

全域設定：

```text
notify_on_completion: true
recipient: x10640305@gmail.com
subject: Codex 工作已完成
```

只要任務曾改動受任務控制檔案，最終回覆前須以已連結 Gmail 通知一次；成功／失敗、只改 PEICD 亦同；完全未改檔不寄。

規則：

- 受任務控制檔案：專案、PEICD、指定輸出路徑及代理／命令直接建立、修改、刪除、移動、改名、覆蓋或轉換者；平台／OS／工具不可控快取除外。即使回滾、刪暫存、blocked／cancelled 或驗證失敗仍通知。
- 同一 `task_id` 最多一次；無 `task_id` 時同一要求最多一次。正文只列安全相對路徑、變更摘要、結果／驗證／回滾、下一步與手動事項。
- 所有通知 Gmail 正文最後一個非空白行必須單獨為 `【CODEX】`；其後不得有代理產生的任何內容。寄送前須驗證待送正文，不符不得送出。
- 不附 `codex_compressed`，除非明確要求；禁秘密、token、私人絕對路徑、個資、完整 log／大段內容。Gmail 工具不可用／被拒／失敗時不重試、不回滾、不阻擋，最終回覆說明；取消優先。
- Gmail、`codex_compressed`、通知 evidence／後置標記不得再次觸發通知／打包；必要紀錄放不遞迴 evidence，禁迴圈。

---
### 2.5 安全、檔案與 Git
#### 2.5.1 敏感資料與 `.gitignore`

- 真實 token、cookie、密碼、私鑰、憑證等 secrets 永不提交；只留去識別範例／外部 Secret Store。誤入 tracked／memory／output 時停止擴散並回報，未授權不撤銷或輪替外部憑證。
- 大型、可重建或持續增長產物一律進專案根 `.gitignore`，如影音、權重、資料集、Cache／Build／Temp／大 Log／壓縮包、`.peicd100/codex/{tmp,artifacts}/`、`.peicd100/codex_compressed/`；無 Git 也可預先維護。
- 一般隱私資料先判 Repo visibility：無 worktree／Remote=`PRIVATE_LOCAL`；任一可推送 Remote 經官方 CLI／API 證 Public=`PUBLIC`；全部證 Private／Internal=`PRIVATE`；不可只看 URL，GitHub 可用 `gh repo view --json visibility`。`UNKNOWN` 時不得新增提交／push 隱私資料。
- `PUBLIC` 必須忽略 `.peicd100/codex/` 與所有非公開／個資／本機路徑／內部 Log／設定，公開或 PR 前核對 tracked files；`PRIVATE／PRIVATE_LOCAL` 可按需追蹤一般隱私，但 secrets／大型產物仍禁。改 Public 前跑 Public Exposure Gate：`.gitignore`＋`git ls-files`／`git check-ignore -v`／`git status --ignored`。
- `.gitignore` 不移除已追蹤檔；`git rm --cached`、history rewrite、force push 未經明確授權不得做。
#### 2.5.2 修改與 Git

- 先理解再改；除最小 bootstrap 外，未要求不建立／移動／刪除／覆蓋持久產物。尊重既有變更，不還原他人修改；手動修改優先 patch，機械整理才用腳本。
- 修改前後查 Git status，區分代理／使用者變更。不自動 commit、push、PR、部署、發布、上傳，除非明確要求。
- `git reset --hard`、`git checkout --`、`git clean -fd`、force push 等破壞性操作需明確授權；大量刪除／覆蓋／搬移／不可逆遷移前先有可驗證回復方案。

---
### 2.6 驗證、品質與環境
#### 2.6.1 一般驗證

- 修改後依慣例跑最小可靠 format／lint／typecheck／test／build／smoke；修 bug 優先補回歸測試。無法驗證須明列原因、範圍、剩餘風險，不得推測成功。
- 不平行爭用同一 cache／GPU／DB／Registry transaction／測試資源；UI 可行時視覺檢查。產物驗存在、大小、格式、可開啟性、關鍵內容；壓縮後驗清單。完成前逐項核 acceptance criteria，未完成必列。
#### 2.6.2 事實查證

- 可主動搜尋網頁／社群；可變外部資訊優先官方／第一手，技術問題優先官方文件、原始 repo、規格／論文；社群只補風險，不取代官方或本地證據。
- 本地事實以 repo／設定／鎖定檔／原始碼／測試／runtime 為準；未驗證的使用者陳述只算輸入／觀察，不升格 verified fact。
#### 2.6.3 使用者環境與偏好

Windows 11、Intel i9、RTX 4070 12GB、RAM 64GB。Python 優先專案既有 Conda，無記錄時可用 `PEICD100`；不得只因 Windows Store alias 另裝。GPU 加速須驗證並有 fallback。長 CLI 沿用既有進度顯示；流程與架構適合時優先 Mermaid；深色 UI 只在明確要求時使用。
#### 2.6.4 VBS／BAT 雙層入口

僅使用者明確要求「VBS／VSB 入口」或 Canonical 已啟用時適用；內部測試／validator／一次性 `.py` 不建。

- 統一放 `.peicd100/vbs_bat/`，同入口同 basename `.bat + .vbs`；多入口用 README／manifest 記目標 `.py`、Conda、參數、用途。
- `.vbs` 僅用 `WScript.Shell` 啟動 `.bat`，正確引用空白／非 ASCII 路徑，不複製 Python 邏輯；`.bat` 從 `%~dp0` 解析專案根，依 PEICD／Canonical 選 Conda，無記錄才 `PEICD100`，轉交 `%*` 並保留 cwd／log／`errorlevel`，不硬編碼個人路徑。
- 目標／環境／參數／輸出／啟動方式變更時同步兩層與 manifest；退役移除或 deprecated。驗 `.bat`、`.vbs`、空白／中文路徑、參數、interpreter、cwd、輸出／log、失敗 exit code；無 GUI 時做靜態＋`.bat` smoke test並明列缺口。

---
### 2.7 完成回報與本文件維護
#### 2.7.1 最終回覆

說明完成內容、產物／驗證、重用／跳過、未完成／風險、唯一下一步、手動事項、檔案變更及 `codex_compressed`／Gmail 狀態；不得虛稱驗證／打包／寄送或貼大量 log，交付前確認檔案可用。
#### 2.7.2 本文件維護

本檔只留跨專案不變量；細節下沉 PEICD／工具。新增規則先找 semantic owner，能 `update／merge／replace` 就不另加同義條款；每概念只留一個 active owner，例子／Schema／長流程下沉，避免多權威。目標 26～28 KiB、<30 KiB。修改後驗版本／大小／矛盾／秘密／fence／UTF-8，並 QA single-writer、last-clean、transaction/digest、memory budget、Canonical↔Effective Policy authority convergence、Recovery、Unknown child、atomic promotion、`codex_compressed`／LATEST、Gmail 防迴圈、指令預算；Codex run 驗首尾載入且無重複。
