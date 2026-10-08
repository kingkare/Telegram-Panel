import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import ts from 'typescript'

const source = await readFile(new URL('../src/utils/loginEmailDisplay.ts', import.meta.url), 'utf8')
const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext } }).outputText
const { loginEmailDisplay } = await import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`)

test('匹配云端掩码的本地邮箱直接完整显示', () => {
  assert.deepEqual(loginEmailDisplay({ success: true, hasLoginEmail: true, loginEmailPattern: 'a***@example.com', loginEmail: 'alice@example.com', verificationStatus: 'verified' }), {
    text: '已启用：alice@example.com', hint: '',
  })
})

test('云端查询失败时保留完整邮箱并标注未核验', () => {
  const result = loginEmailDisplay({ success: false, error: '超时', hasLoginEmail: true, loginEmail: 'alice@example.com', verificationStatus: 'unverified' })
  assert.equal(result.text, 'alice@example.com（未核验）')
  assert.match(result.hint, /未能完成云端核验/)
})

test('没有有效本地记录时展示云端掩码，不能展示失效的地址', () => {
  const result = loginEmailDisplay({ success: true, hasLoginEmail: true, loginEmailPattern: 'b***@example.com', loginEmail: 'alice@example.com', verificationStatus: 'unavailable' })
  assert.equal(result.text, '已启用：b***@example.com')
  assert.match(result.hint, /暂无匹配/)
})

test('无邮箱、无记录或查询失败不会显示伪造的完整地址', () => {
  assert.equal(loginEmailDisplay({ success: true, hasLoginEmail: false }).text, '未启用')
  assert.equal(loginEmailDisplay(null).text, '查询失败')
  assert.equal(loginEmailDisplay({ success: false, error: '连接失败' }).text, '连接失败')
})
