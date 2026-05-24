import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer } from "recharts";
import type { ResourceTickerData } from "../tickers/ResourceTickerCollection";
import { formatResourceAmount } from "@/util/number";

interface RegionProduction {
  name: string;
  thisTurn: number;
  lastTurn: number;
}

interface ProductionHistory {
  turn: number;
  production: number;
  demand: number;
}

const dummyTopRegions: RegionProduction[] = [
  { name: "Ironholm Vale", thisTurn: 1240, lastTurn: 1180 },
  { name: "Ashwood Basin", thisTurn: 1105, lastTurn: 1090 },
  { name: "Duskmarsh Plains", thisTurn: 980, lastTurn: 1020 },
  { name: "Sunken Reach", thisTurn: 875, lastTurn: 860 },
  { name: "Graywood Highlands", thisTurn: 810, lastTurn: 795 },
];

const dummyHistory: ProductionHistory[] = [
  { turn: 1, production: 4200, demand: 5100 },
  { turn: 2, production: 4380, demand: 5080 },
  { turn: 3, production: 4150, demand: 5200 },
  { turn: 4, production: 4520, demand: 5150 },
  { turn: 5, production: 4700, demand: 5300 },
  { turn: 6, production: 4890, demand: 5250 },
  { turn: 7, production: 4670, demand: 5400 },
  { turn: 8, production: 5010, demand: 5380 },
  { turn: 9, production: 5230, demand: 5420 },
  { turn: 10, production: 5140, demand: 5500 },
  { turn: 11, production: 5400, demand: 5480 },
  { turn: 12, production: 5610, demand: 5600 },
  { turn: 13, production: 5480, demand: 5700 },
  { turn: 14, production: 5820, demand: 5650 },
  { turn: 15, production: 6040, demand: 5800 },
];

interface ResourceManagerProductionPageProps {
  selectedTicker: ResourceTickerData | null;
  totalThisTurn: number;
  totalLastTurn: number;
}

export default function ResourceManagerProductionPage({ selectedTicker: _, totalThisTurn, totalLastTurn }: ResourceManagerProductionPageProps) {
  return (
    <div className="flex flex-col gap-6 p-2">
      {/* Top 5 producing regions */}
      <section>
        <h3 className="text-sm font-semibold mb-2">Top Producing Regions</h3>
        <div className="overflow-x-auto rounded-md border border-slate-300">
          <table className="min-w-full text-sm text-left">
            <thead className="bg-slate-100 text-black uppercase text-xs tracking-widest">
              <tr>
                <th className="px-3 py-2 font-medium">Region</th>
                <th className="px-3 py-2 text-right font-medium">This Turn</th>
                <th className="px-3 py-2 text-right font-medium">Last Turn</th>
              </tr>
            </thead>
            <tbody>
              {dummyTopRegions.map((row) => {
                const delta = row.thisTurn - row.lastTurn;
                return (
                  <tr key={row.name}>
                    <td className="px-3 py-2 text-black">{row.name}</td>
                    <td className="px-3 py-2 text-right font-mono text-black">{formatResourceAmount(row.thisTurn)}</td>
                    <td className="px-3 py-2 text-right font-mono">
                      <span className="text-black">{formatResourceAmount(row.lastTurn)}</span>
                      {delta !== 0 && (
                        <span className={`ml-2 text-xs ${delta > 0 ? "text-emerald-400" : "text-rose-400"}`}>
                          {delta > 0 ? "▲" : "▼"} {Math.abs(delta)}
                        </span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot className="border-t-2 border-slate-300 bg-slate-50 font-semibold text-sm">
              <tr>
                <td className="px-3 py-2 text-black">Total</td>
                <td className="px-3 py-2 text-right font-mono text-black">{formatResourceAmount(totalThisTurn)}</td>
                <td className="px-3 py-2 text-right font-mono text-black">{formatResourceAmount(totalLastTurn)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </section>

      {/* Production vs Demand over time */}
      <section>
        <h3 className="text-sm font-semibold mb-2">Production vs Demand Over Time</h3>
        <div className="h-56">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={dummyHistory} margin={{ top: 4, right: 16, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
              <XAxis
                dataKey="turn"
                tick={{ fill: "#94a3b8", fontSize: 11 }}
                label={{ value: "Turn", position: "insideBottomRight", offset: -4, fill: "#64748b", fontSize: 11 }}
              />
              <YAxis
                tick={{ fill: "#94a3b8", fontSize: 11 }}
                width={48}
                tickFormatter={(v: number) => (v >= 1000 ? `${(v / 1000).toFixed(1)}k` : String(v))}
              />
              <Tooltip
                contentStyle={{ backgroundColor: "#0f172a", border: "1px solid #334155", borderRadius: 6, fontSize: 12 }}
                labelStyle={{ color: "#94a3b8" }}
                itemStyle={{ color: "#e2e8f0" }}
                formatter={(value, name) => [Number(value).toLocaleString(), name]}
                labelFormatter={(label) => `Turn ${label}`}
              />
              <Legend wrapperStyle={{ fontSize: 12, color: "#94a3b8", paddingTop: 6 }} />
              <Line
                type="monotone"
                dataKey="production"
                name="Production"
                stroke="#34d399"
                strokeWidth={2}
                dot={{ r: 3, fill: "#34d399" }}
                activeDot={{ r: 5 }}
              />
              <Line
                type="monotone"
                dataKey="demand"
                name="Demand"
                stroke="#f87171"
                strokeWidth={2}
                strokeDasharray="5 3"
                dot={{ r: 3, fill: "#f87171" }}
                activeDot={{ r: 5 }}
              />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </section>
    </div>
  );
}
