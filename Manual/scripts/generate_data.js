const data = new Array(8).fill(0).map(() => new Array(10).fill(0).map(() => Math.floor(Math.random() * 10)));
for (const row of data) console.log(row.join(""));
