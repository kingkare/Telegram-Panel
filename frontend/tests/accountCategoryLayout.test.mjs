import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const source = await readFile(new URL('../src/views/AccountCategories.vue', import.meta.url), 'utf8')

test('账号分类页只展示分类表和分类维护操作', () => {
  assert.equal((source.match(/<el-table\s/g) || []).length, 1)
  assert.match(source, /:data="categories"/)
  assert.match(source, /分类管理/)
  assert.match(source, /添加分类/)
  assert.match(source, /编辑分类/)
  assert.match(source, /@click="deleteCategory\(row\)"/)
  assert.doesNotMatch(source, /账号批量改分类|账号信息|手机号|全选当前筛选|category-account-toolbar/)
})

test('分类维护仅刷新分类数据，不请求账号明细', () => {
  assert.match(source, /onMounted\(loadCategories\)/)
  assert.match(source, /panelApi\.accountCategories\(\)/)
  assert.match(source, /panelApi\.createAccountCategory\(/)
  assert.match(source, /panelApi\.updateAccountCategory\(/)
  assert.match(source, /panelApi\.deleteAccountCategory\(/)
  assert.doesNotMatch(source, /panelApi\.(accounts|batchSetAccountCategory)\(|loadAllAccounts|AccountListItem/)
})

test('账号分类页移动端创建表单会纵向收缩', () => {
  assert.match(source, /@media \(max-width: 720px\)/)
  assert.match(source, /grid-template-columns: 1fr;/)
})
