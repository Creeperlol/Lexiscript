## 關於code comment 規則

**只寫 WHY，不寫 WHAT。** 程式碼能自我說明的就不加註解。

### 該寫
- 非直覺的 API / 函式庫行為（例：回傳 lazy iterator，需迭代才會真正執行）
- 技術限制或 workaround（例：繞過特定版本 bug、路徑解析 hack）
- 硬體 / 業務上下文（例：連線逾時 = 裝置端已結束錄音）
- 用途不直覺的常數（例：24 KB 上限是為了塞滿對端 64 KB buffer）
- 與名稱語意有落差的呼叫（例：預熱模型、刻意的副作用）

### 不寫
- 區塊分隔標題（`Paths`、`Network Settings`）
- 重述宣告或呼叫（`Initialize client`、`Singleton instance`）
- 步驟編號（`1. ASR`、`2. LLM`），log 已能說明流程
- 與 docstring / doc comment 重複的說明

### 其他
- 不寫變更紀錄（`改用 X`、`Fixed: ...`），那屬於 commit message
- 不留被註解掉的舊程式碼
- 本規則只管 inline comment，doc comment 照常撰寫
- 不主動刪改既有註解，只套用在新寫的程式碼