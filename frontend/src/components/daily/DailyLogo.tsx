interface DailyLogoProps {
  size?: number;
}

export function DailyLogo({ size = 16 }: DailyLogoProps) {
  return (
    <svg width={size} height={size} viewBox="0 0 18 18" fill="none" aria-hidden="true">
      <circle cx="9" cy="9" r="3" fill="white" opacity="0.9" />
      <circle cx="9" cy="2.5" r="1.5" fill="white" opacity="0.6" />
      <circle cx="9" cy="15.5" r="1.5" fill="white" opacity="0.6" />
      <circle cx="2.5" cy="9" r="1.5" fill="white" opacity="0.6" />
      <circle cx="15.5" cy="9" r="1.5" fill="white" opacity="0.6" />
      <line x1="9" y1="4" x2="9" y2="6" stroke="white" strokeWidth="1" opacity="0.45" />
      <line x1="9" y1="12" x2="9" y2="14" stroke="white" strokeWidth="1" opacity="0.45" />
      <line x1="4" y1="9" x2="6" y2="9" stroke="white" strokeWidth="1" opacity="0.45" />
      <line x1="12" y1="9" x2="14" y2="9" stroke="white" strokeWidth="1" opacity="0.45" />
    </svg>
  );
}