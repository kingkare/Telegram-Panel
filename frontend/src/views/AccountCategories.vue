<template>
  <div class="category-page">
    <el-card shadow="never" class="page-card">
      <template #header>
        <div class="card-header category-header">
          <div>
            <div class="card-title">分类管理</div>
            <div class="muted">维护分类名称、描述和“是否排除在创建/批量任务之外”。</div>
          </div>
          <el-button :icon="Refresh" :loading="loading" @click="loadCategories">刷新分类</el-button>
        </div>
      </template>

      <el-form label-position="top" class="category-create-form">
        <el-form-item label="分类名称" class="category-name-field">
          <el-input v-model="createForm.name" placeholder="例如：AI广告" />
        </el-form-item>
        <el-form-item label="描述" class="category-description-field">
          <el-input v-model="createForm.description" type="textarea" :rows="2" placeholder="可选，说明此分类用途" />
        </el-form-item>
        <el-form-item label="操作排除" class="category-exclude-field">
          <el-checkbox v-model="createForm.excludeFromOperations">不出现在创建/批量任务中</el-checkbox>
        </el-form-item>
        <el-form-item class="category-submit-field">
          <el-button type="primary" class="full-btn" :icon="Plus" :disabled="!createForm.name.trim()" :loading="creating" @click="createCategory">
            添加分类
          </el-button>
        </el-form-item>
      </el-form>

      <el-table v-loading="loading" :data="categories" stripe class="mt-4">
        <el-table-column label="分类名称" min-width="150">
          <template #default="{ row }">
            <el-tag effect="plain" class="category-name-tag" :style="accountCategoryTagStyle(row)">
              {{ row.name }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="描述" min-width="180">
          <template #default="{ row }">{{ row.description || '-' }}</template>
        </el-table-column>
        <el-table-column label="排除操作" width="110">
          <template #default="{ row }">
            <el-tag v-if="row.excludeFromOperations" type="warning" size="small">是</el-tag>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="accountCount" label="账号数量" width="100" />
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" :icon="Edit" title="编辑" @click="openEdit(row)" />
            <el-button link type="danger" :icon="Delete" title="删除" @click="deleteCategory(row)" />
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="editDialog.visible" title="编辑分类" width="min(460px, calc(100vw - 24px))">
      <el-form label-position="top">
        <el-form-item label="分类名称">
          <el-input v-model="editDialog.form.name" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="editDialog.form.description" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item>
          <el-checkbox v-model="editDialog.form.excludeFromOperations">排除操作（不出现在创建/批量任务中）</el-checkbox>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editDialog.visible = false">取消</el-button>
        <el-button type="primary" :disabled="!editDialog.form.name.trim()" :loading="editDialog.saving" @click="saveEdit">
          保存
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Edit, Plus, Refresh } from '@element-plus/icons-vue'
import { panelApi } from '@/api/panel'
import type { AccountCategory } from '@/api/types'
import { accountCategoryTagStyle } from '@/utils/categoryStyle'

const categories = ref<AccountCategory[]>([])
const loading = ref(false)
const creating = ref(false)

const createForm = reactive({
  name: '',
  description: '',
  excludeFromOperations: false,
})

const editDialog = reactive({
  visible: false,
  saving: false,
  id: 0,
  form: {
    name: '',
    description: '',
    excludeFromOperations: false,
  },
})

async function loadCategories() {
  loading.value = true
  try {
    categories.value = await panelApi.accountCategories()
  } finally {
    loading.value = false
  }
}

async function createCategory() {
  creating.value = true
  try {
    const saved = await panelApi.createAccountCategory({
      name: createForm.name,
      color: null,
      description: createForm.description,
      excludeFromOperations: createForm.excludeFromOperations,
    })
    ElMessage.success(`分类 "${saved.name}" 添加成功`)
    createForm.name = ''
    createForm.description = ''
    createForm.excludeFromOperations = false
    await loadCategories()
  } finally {
    creating.value = false
  }
}

function openEdit(category: AccountCategory) {
  editDialog.id = category.id
  editDialog.form.name = category.name
  editDialog.form.description = category.description || ''
  editDialog.form.excludeFromOperations = category.excludeFromOperations
  editDialog.visible = true
}

async function saveEdit() {
  editDialog.saving = true
  try {
    await panelApi.updateAccountCategory(editDialog.id, {
      name: editDialog.form.name,
      color: null,
      description: editDialog.form.description,
      excludeFromOperations: editDialog.form.excludeFromOperations,
    })
    ElMessage.success('分类已更新')
    editDialog.visible = false
    await loadCategories()
  } finally {
    editDialog.saving = false
  }
}

async function deleteCategory(category: AccountCategory) {
  await ElMessageBox.confirm(`确定要删除分类 ${category.name} 吗？关联的账号将变为未分类。`, '确认删除', {
    type: 'warning',
    confirmButtonText: '删除',
    cancelButtonText: '取消',
  })
  await panelApi.deleteAccountCategory(category.id)
  ElMessage.success('删除成功')
  await loadCategories()
}

onMounted(loadCategories)
</script>

<style scoped>
.category-page {
  width: min(100%, 1536px);
  margin: 0 auto;
}

.category-page .page-card {
  width: 100%;
  margin-left: 0;
  margin-right: 0;
}

.category-header {
  align-items: flex-start;
  gap: 12px;
}

.card-title {
  font-weight: 600;
  line-height: 1.5;
}

.category-create-form {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(320px, 1.45fr) minmax(220px, 0.9fr) minmax(120px, auto);
  gap: 12px;
  align-items: end;
}

.category-create-form :deep(.el-form-item) {
  margin-bottom: 0;
}

.category-submit-field :deep(.el-form-item__content) {
  align-items: end;
}

.full-btn {
  width: 100%;
}

.category-name-tag {
  border-radius: 999px;
}

@media (max-width: 1180px) {
  .category-create-form {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
}

@media (max-width: 720px) {
  .category-create-form {
    grid-template-columns: 1fr;
  }
}
</style>
