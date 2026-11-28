import { planJobs, runJob, verifyConcurrently } from "./verify-jobs.mjs";

// Runs every verification job in this engine, one after another. scripts/test.mjs runs the
// same jobs concurrently through worker threads and browser contexts. Both paths share
// planJobs() and verifyConcurrently(), so they make the same assertions and report the same summary.
export function verify(load, cases, saveCompiler) {
    const jobs = planJobs(cases, 1, 1);
    let chain = Promise.resolve();
    // Sequential: each job starts after the previous one settles.
    const execute = job => (chain = chain.then(() => runJob(job, load, cases), () => runJob(job, load, cases)));
    return verifyConcurrently(cases, jobs, execute, saveCompiler);
}
