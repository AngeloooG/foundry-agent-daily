export default function NeuralNetwork({ dark }: { dark: boolean }) {
  const nodes = [
    { id: 'c',  cx: 200, cy: 200, r: 12, cls: '' },
    { id: 'a1', cx: 60,  cy: 80,  r: 7,  cls: 'node-float' },
    { id: 'a2', cx: 340, cy: 60,  r: 7,  cls: 'node-float-2' },
    { id: 'a3', cx: 60,  cy: 320, r: 7,  cls: 'node-float-3' },
    { id: 'a4', cx: 340, cy: 330, r: 7,  cls: 'node-float' },
    { id: 'a5', cx: 190, cy: 40,  r: 5,  cls: 'node-float-2' },
    { id: 'a6', cx: 380, cy: 185, r: 5,  cls: 'node-float-3' },
    { id: 'a7', cx: 20,  cy: 195, r: 5,  cls: 'node-float' },
    { id: 'a8', cx: 195, cy: 360, r: 5,  cls: 'node-float-2' },
    { id: 'b1', cx: 120, cy: 140, r: 4,  cls: 'node-float-3' },
    { id: 'b2', cx: 275, cy: 120, r: 4,  cls: 'node-float' },
    { id: 'b3', cx: 280, cy: 270, r: 4,  cls: 'node-float-2' },
    { id: 'b4', cx: 115, cy: 265, r: 4,  cls: 'node-float-3' },
  ]

  const edges = [
    ['c','a1'],['c','a2'],['c','a3'],['c','a4'],
    ['c','a5'],['c','a6'],['c','a7'],['c','a8'],
    ['c','b1'],['c','b2'],['c','b3'],['c','b4'],
    ['a1','b1'],['a1','a5'],['a2','b2'],['a2','a5'],
    ['a3','b4'],['a4','b3'],['b1','b4'],['b2','b3'],
    ['a6','b2'],['a7','b1'],['a8','b3'],['a8','b4'],
    ['a5','b2'],
  ]

  const stroke = dark ? 'rgba(124,188,227,0.18)' : 'rgba(0,91,150,0.12)'
  const nodeFill = dark ? 'rgba(0,91,150,0.6)' : 'rgba(0,91,150,0.45)'
  const nodeBorder = dark ? 'rgba(124,188,227,0.55)' : 'rgba(0,91,150,0.4)'
  const centerFill = dark
    ? 'url(#centerGrad)'
    : 'url(#centerGradLight)'

  return (
    <svg
      viewBox="0 0 400 400"
      width="100%"
      height="100%"
      style={{ overflow: 'visible' }}
      aria-hidden="true"
    >
      <defs>
        <radialGradient id="centerGrad" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#38BDF8" stopOpacity="0.9" />
          <stop offset="60%" stopColor="#005B96" stopOpacity="0.85" />
          <stop offset="100%" stopColor="#123263" stopOpacity="0.7" />
        </radialGradient>
        <radialGradient id="centerGradLight" cx="50%" cy="50%" r="50%">
          <stop offset="0%" stopColor="#7CBCE3" stopOpacity="0.9" />
          <stop offset="60%" stopColor="#005B96" stopOpacity="0.85" />
          <stop offset="100%" stopColor="#123263" stopOpacity="0.75" />
        </radialGradient>
        <filter id="glow">
          <feGaussianBlur stdDeviation="3" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
        <filter id="glow-strong">
          <feGaussianBlur stdDeviation="6" result="blur" />
          <feMerge><feMergeNode in="blur" /><feMergeNode in="SourceGraphic" /></feMerge>
        </filter>
      </defs>

      {/* Edges */}
      {edges.map(([from, to], i) => {
        const a = nodes.find(n => n.id === from)!
        const b = nodes.find(n => n.id === to)!
        return (
          <line
            key={i}
            x1={a.cx} y1={a.cy}
            x2={b.cx} y2={b.cy}
            stroke={stroke}
            strokeWidth={a.id === 'c' || b.id === 'c' ? 1.2 : 0.7}
          />
        )
      })}

      {/* Outer nodes */}
      {nodes.filter(n => n.id !== 'c').map((n) => (
        <g key={n.id} className={n.cls}>
          <circle
            cx={n.cx} cy={n.cy} r={n.r + 4}
            fill={dark ? 'rgba(56,189,248,0.05)' : 'rgba(0,91,150,0.04)'}
          />
          <circle
            cx={n.cx} cy={n.cy} r={n.r}
            fill={nodeFill}
            stroke={nodeBorder}
            strokeWidth={0.8}
            filter="url(#glow)"
          />
        </g>
      ))}

      {/* Center node — pulse rings */}
      <circle
        cx={200} cy={200} r={28}
        fill="none"
        stroke={dark ? 'rgba(56,189,248,0.18)' : 'rgba(0,91,150,0.12)'}
        strokeWidth={1}
        style={{ animation: 'pulse-ring 2.8s 0s ease-out infinite' }}
      />
      <circle
        cx={200} cy={200} r={22}
        fill="none"
        stroke={dark ? 'rgba(56,189,248,0.14)' : 'rgba(0,91,150,0.09)'}
        strokeWidth={1}
        style={{ animation: 'pulse-ring 2.8s 1.4s ease-out infinite' }}
      />
      <circle
        cx={200} cy={200} r={12}
        fill={centerFill}
        filter="url(#glow-strong)"
      />
      {/* Center icon */}
      <g transform="translate(200,200)" style={{ pointerEvents: 'none' }}>
        <circle cx={0} cy={0} r={3.5} fill="white" opacity={0.9} />
        {[[-8,0],[8,0],[0,-8],[0,8]].map(([dx,dy],i) => (
          <g key={i}>
            <line x1={dx*0.45} y1={dy*0.45} x2={dx*0.78} y2={dy*0.78} stroke="white" strokeWidth={0.9} opacity={0.5} />
            <circle cx={dx} cy={dy} r={1.8} fill="white" opacity={0.65} />
          </g>
        ))}
      </g>
    </svg>
  )
}
