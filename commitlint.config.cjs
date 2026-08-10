module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // allow sentence-case subjects (e.g. dependabot's "Bump x from y to z"); still blocks start/pascal/upper-case
    'subject-case': [2, 'never', ['start-case', 'pascal-case', 'upper-case']],
  },
};
