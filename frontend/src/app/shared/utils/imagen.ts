/**
 * Dimensiones de salida que respeta el lado mayor: nunca amplía, solo reduce.
 * Es la parte pura de la compresión (se prueba sin canvas).
 */
export function dimensionesEscaladas(
  ancho: number,
  alto: number,
  maxLado: number,
): { ancho: number; alto: number } {
  const escala = Math.min(1, maxLado / Math.max(ancho, alto));
  return { ancho: Math.round(ancho * escala), alto: Math.round(alto * escala) };
}

/**
 * Comprime una imagen del dispositivo a un Blob JPEG (lado máx. y calidad
 * acotados) para subirla al endpoint de imágenes sin cargar el archivo original
 * (el API acepta hasta 5 MB). Si el navegador no puede dibujarla en canvas
 * devuelve el archivo original.
 */
export async function comprimirImagen(file: File, maxLado = 1280, calidad = 0.7): Promise<Blob> {
  const dataUrl = await new Promise<string>((resolve, reject) => {
    const lector = new FileReader();
    lector.onload = () => resolve(lector.result as string);
    lector.onerror = () => reject(new Error('No se pudo leer la imagen'));
    lector.readAsDataURL(file);
  });
  const img = await new Promise<HTMLImageElement>((resolve, reject) => {
    const imagen = new Image();
    imagen.onload = () => resolve(imagen);
    imagen.onerror = () => reject(new Error('Imagen inválida'));
    imagen.src = dataUrl;
  });
  const { ancho, alto } = dimensionesEscaladas(img.width, img.height, maxLado);
  const canvas = document.createElement('canvas');
  canvas.width = ancho;
  canvas.height = alto;
  const ctx = canvas.getContext('2d');
  if (!ctx) return file;
  ctx.drawImage(img, 0, 0, ancho, alto);
  return await new Promise<Blob>((resolve, reject) =>
    canvas.toBlob(
      (b) => (b ? resolve(b) : reject(new Error('No se pudo comprimir'))),
      'image/jpeg',
      calidad,
    ),
  );
}
