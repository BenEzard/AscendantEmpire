export function safeStringify(value: unknown, space = 2): string {
  const seen = new WeakSet<object>();
  const output = JSON.stringify(
    value,
    (_k, v: unknown) => {
      if (typeof v === "bigint") return v.toString();
      if (v instanceof Date) return v.toISOString();
      if (typeof v === "object" && v !== null) {
        if (seen.has(v)) return "[Circular]";
        seen.add(v);
      }
      return v;
    },
    space
  ) ?? "";

  console.log("*** output: "+output)

  return output;
}