import type { LoginEmailStatus } from '@/api/types'

export function loginEmailDisplay(status: LoginEmailStatus | null): { text: string; hint: string } {
  if (!status) return { text: '查询失败', hint: '' }
  // 查询失败仍可展示服务端保留的已确认地址，但必须标明尚未核验。
  if (status.loginEmail && status.verificationStatus === 'unverified') {
    return { text: `${status.loginEmail}（未核验）`, hint: '显示本地已确认的邮箱，本次未能完成云端核验。' }
  }
  if (!status.success) return { text: status.error || '查询失败', hint: '' }
  if (!status.hasLoginEmail) return { text: '未启用', hint: '' }
  if (status.loginEmail && status.verificationStatus === 'verified') {
    return { text: `已启用：${status.loginEmail}`, hint: '' }
  }
  const pattern = status.loginEmailPattern
  return {
    text: pattern ? `已启用：${pattern}` : '已启用',
    hint: pattern?.includes('*') ? 'Telegram 仅返回掩码，暂无匹配的本地完整邮箱记录。' : '',
  }
}
