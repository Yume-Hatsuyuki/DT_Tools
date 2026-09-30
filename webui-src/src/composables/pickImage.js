/** 弹出系统图片选择框，返回选中的 File；用户取消时 resolve(null)。 */
export function pickImageFile(accept = 'image/png,image/jpeg,image/gif,image/webp,image/svg+xml') {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.onchange = () => resolve(input.files && input.files[0]);
    input.click();
  });
}
